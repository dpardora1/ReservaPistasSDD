using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SalaReservas.Api;

namespace SalaReservas.Api.Tests;

public class CancellationServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 6, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void C02_B01_CancelPreservesOriginalDataAndOtherReservations()
    {
        var service = new ReservationService(new Clock());
        var original = Create(service);
        var other = Create(service, "B");
        Assert.False(original.IsCancelled);

        Assert.Equal(CancellationResult.Cancelled, service.Cancel(original.Id));

        Assert.Equal(new[] { original with { IsCancelled = true }, other }, service.GetSnapshot());
        Assert.False(original.IsCancelled);
    }

    [Fact]
    public void C03_B02_CancelledReservationReleasesIntervalWithoutBeingDeleted()
    {
        var service = new ReservationService(new Clock());
        var original = Create(service);
        Assert.Equal(CancellationResult.Cancelled, service.Cancel(original.Id));

        var replacement = Create(service);

        Assert.NotEqual(original.Id, replacement.Id);
        Assert.False(replacement.IsCancelled);
        Assert.Equal(new[] { original with { IsCancelled = true }, replacement }, service.GetSnapshot());
        var before = service.GetSnapshot();
        Assert.Equal(CancellationResult.Cancelled, service.Cancel(original.Id));
        Assert.Equal(before, service.GetSnapshot());
        Assert.Equal(ReservationError.Overlap,
            Assert.IsType<ReservationResult.Rejected>(service.Reserve("A", original.Start, original.End)).Error);
    }

    [Fact]
    public void C01_B03_UnknownReservationDoesNotChangeState()
    {
        var service = new ReservationService(new Clock());
        Create(service);
        var before = service.GetSnapshot();

        Assert.Equal(CancellationResult.NotFound, service.Cancel(Guid.NewGuid()));
        Assert.Equal(before, service.GetSnapshot());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void C04_B04_RepeatedCancellationSucceedsEvenAfterStart(int advanceHours)
    {
        var clock = new Clock();
        var service = new ReservationService(clock);
        var reservation = Create(service);
        Assert.Equal(CancellationResult.Cancelled, service.Cancel(reservation.Id));
        var before = service.GetSnapshot();
        clock.UtcNow = Now.AddHours(advanceHours);
        clock.ReadCount = 0;

        Assert.Equal(CancellationResult.Cancelled, service.Cancel(reservation.Id));
        Assert.Equal(before, service.GetSnapshot());
        Assert.Equal(0, clock.ReadCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(72000000000)]
    public void C05_B05_StartedReservationIsRejectedWithoutChanges(long ticksAfterStart)
    {
        var clock = new Clock();
        var service = new ReservationService(clock);
        var reservation = Create(service);
        var before = service.GetSnapshot();
        clock.UtcNow = reservation.Start.AddTicks(ticksAfterStart);
        clock.ReadCount = 0;

        Assert.Equal(CancellationResult.AlreadyStarted, service.Cancel(reservation.Id));
        Assert.Equal(before, service.GetSnapshot());
        Assert.Equal(1, clock.ReadCount);
    }

    [Fact]
    public void C05_OneTickBeforeStartCanBeCancelledWithOneClockRead()
    {
        var clock = new Clock();
        var service = new ReservationService(clock);
        var reservation = Create(service);
        clock.UtcNow = reservation.Start.AddTicks(-1);
        clock.ReadCount = 0;

        Assert.Equal(CancellationResult.Cancelled, service.Cancel(reservation.Id));
        Assert.True(Assert.Single(service.GetSnapshot()).IsCancelled);
        Assert.Equal(1, clock.ReadCount);
    }

    [Fact]
    public async Task C06_B06_CreationAndCancellationRespectEitherSerialOrder()
    {
        var service = new ReservationService(new Clock());
        var original = Create(service);
        using var barrier = new Barrier(3);
        Task<T> Compete<T>(Func<T> action) => Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Workers did not reach the barrier.");
            return action();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        var cancel = Compete(() => service.Cancel(original.Id));
        var create = Compete(() => service.Reserve("A", original.Start, original.End));
        var coordinated = barrier.SignalAndWait(TimeSpan.FromSeconds(10));
        await Task.WhenAll(cancel, create);
        Assert.True(coordinated);
        Assert.Equal(CancellationResult.Cancelled, await cancel);
        var state = service.GetSnapshot();
        Assert.Equal(original with { IsCancelled = true }, state.Single(r => r.Id == original.Id));
        var result = await create;
        if (result is ReservationResult.Created success)
        {
            Assert.Equal(success.Reservation, Assert.Single(state, r => !r.IsCancelled));
            Assert.Equal(2, state.Length);
        }
        else
        {
            Assert.Equal(ReservationError.Overlap, Assert.IsType<ReservationResult.Rejected>(result).Error);
            Assert.Single(state);
            Create(service);
        }
    }

    [Fact]
    public async Task CreationHttpContractDoesNotExposeInternalState()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new Clock());
            }));
        using var client = factory.CreateClient();
        var start = Now.AddDays(1);
        using var response = await client.PostAsJsonAsync("/reservations", new
        {
            roomId = "A", start = start.ToString("O"), end = start.AddHours(1).ToString("O")
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "end", "id", "roomId", "start" },
            json.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
    }

    private static Reservation Create(ReservationService service, string roomId = "A") =>
        Assert.IsType<ReservationResult.Created>(service.Reserve(roomId, Now.AddHours(2), Now.AddHours(3))).Reservation;

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = Now;
        public int ReadCount { get; set; }
        public override DateTimeOffset GetUtcNow()
        {
            ReadCount++;
            return UtcNow;
        }
    }
}
