using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SalaReservas.Api;

namespace SalaReservas.Api.Tests;

public class ReservationServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 6, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A01_CreatesReservationWithUniqueIdAndUtcData()
    {
        var service = CreateService();
        var start = Now.AddHours(2).ToOffset(TimeSpan.FromHours(2));
        var end = start.AddHours(1);

        var first = Assert.IsType<ReservationResult.Created>(service.Reserve("A", start, end)).Reservation;
        var second = Assert.IsType<ReservationResult.Created>(service.Reserve("A", end, end.AddHours(1))).Reservation;

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("A", first.RoomId);
        Assert.Equal(start, first.Start);
        Assert.Equal(end, first.End);
        Assert.Equal(TimeSpan.Zero, first.Start.Offset);
        Assert.Equal(TimeSpan.Zero, first.End.Offset);
        Assert.Equal(new[] { first, second }, service.GetSnapshot());
    }

    [Theory]
    [InlineData(10, 30, 11, 30)]
    [InlineData(9, 30, 10, 30)]
    [InlineData(9, 0, 12, 0)]
    [InlineData(10, 15, 10, 45)]
    [InlineData(10, 0, 11, 0)]
    public void R05_A02_A04_OverlapIsRejectedWithoutChangingState(int sh, int sm, int eh, int em)
    {
        var service = CreateService();
        Assert.IsType<ReservationResult.Created>(service.Reserve("A", At(10), At(11)));
        var before = service.GetSnapshot();

        var result = service.Reserve("A", At(sh, sm), At(eh, em));

        Assert.Equal(ReservationError.Overlap, Assert.IsType<ReservationResult.Rejected>(result).Error);
        Assert.Equal(before, service.GetSnapshot());
    }

    [Theory]
    [InlineData(9, 10)]
    [InlineData(11, 12)]
    public void R05_A03_AdjacentReservationsAreAccepted(int startHour, int endHour)
    {
        var service = CreateService();
        Assert.IsType<ReservationResult.Created>(service.Reserve("A", At(10), At(11)));

        Assert.IsType<ReservationResult.Created>(service.Reserve("A", At(startHour), At(endHour)));
        Assert.Equal(2, service.GetSnapshot().Length);
    }

    [Fact]
    public void R06_A05_DifferentRoomsCanUseTheSameInterval()
    {
        var service = CreateService();

        Assert.IsType<ReservationResult.Created>(service.Reserve("A", At(10), At(11)));
        Assert.IsType<ReservationResult.Created>(service.Reserve("B", At(10), At(11)));
        Assert.Equal(2, service.GetSnapshot().Length);
    }

    [Theory]
    [InlineData("missing", 2, 3, ReservationError.RoomNotFound)]
    [InlineData("a", 2, 3, ReservationError.RoomNotFound)]
    [InlineData("A", 2, 2, ReservationError.InvalidInterval)]
    [InlineData("A", 3, 2, ReservationError.InvalidInterval)]
    [InlineData("A", -1, 1, ReservationError.StartInPast)]
    public void R01_R03_R04_A06_A07_A08_RejectionsPreserveExistingReservations(
        string room, int startHours, int endHours, ReservationError expected)
    {
        var service = CreateService();
        Assert.IsType<ReservationResult.Created>(service.Reserve("B", At(14), At(15)));
        var before = service.GetSnapshot();

        var result = service.Reserve(room, Now.AddHours(startHours), Now.AddHours(endHours));

        Assert.Equal(expected, Assert.IsType<ReservationResult.Rejected>(result).Error);
        Assert.Equal(before, service.GetSnapshot());
    }

    [Fact]
    public void R04_StartAtNowAndOneTickDurationAreAcceptedWithOneClockRead()
    {
        var clock = new ControlledTimeProvider(Now);
        var service = new ReservationService(clock);

        Assert.IsType<ReservationResult.Created>(service.Reserve("A", Now, Now.AddTicks(1)));
        Assert.Equal(1, clock.ReadCount);
    }

    [Fact]
    public void R04_OneTickBeforeNowIsRejected()
    {
        var service = CreateService();

        var result = service.Reserve("A", Now.AddTicks(-1), Now.AddHours(1));

        Assert.Equal(ReservationError.StartInPast, Assert.IsType<ReservationResult.Rejected>(result).Error);
        Assert.Empty(service.GetSnapshot());
    }

    [Fact]
    public void R04_EvaluationUsesCurrentClockRatherThanServiceCreationTime()
    {
        var clock = new ControlledTimeProvider(Now);
        var service = new ReservationService(clock);
        clock.UtcNow = Now.AddHours(3);

        var result = service.Reserve("A", At(10), At(12));

        Assert.Equal(ReservationError.StartInPast, Assert.IsType<ReservationResult.Rejected>(result).Error);
        Assert.Empty(service.GetSnapshot());
    }

    [Fact]
    public void R05_OverlapIsComparedAsInstantsNotLocalClockValues()
    {
        var service = CreateService();
        Assert.IsType<ReservationResult.Created>(service.Reserve("A", At(10), At(11)));

        var result = service.Reserve("A", At(10).ToOffset(TimeSpan.FromHours(-5)),
            At(11).ToOffset(TimeSpan.FromHours(-5)));

        Assert.Equal(ReservationError.Overlap, Assert.IsType<ReservationResult.Rejected>(result).Error);
        Assert.Single(service.GetSnapshot());
    }

    [Fact]
    public void NoArtificialDurationOrAdvanceLimitIsApplied()
    {
        var service = CreateService();

        Assert.IsType<ReservationResult.Created>(service.Reserve("A", Now, Now.AddYears(50)));
        Assert.IsType<ReservationResult.Created>(service.Reserve("B", Now.AddYears(100), DateTimeOffset.MaxValue));
    }

    [Fact]
    public async Task R07_A10_ConcurrentRequestsCreateExactlyOneReservation()
    {
        var service = CreateService();
        using var barrier = new Barrier(3);

        Task<ReservationResult> Compete() => Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Concurrent workers did not reach the barrier.");

            return service.Reserve("A", At(10), At(11));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        var first = Compete();
        var second = Compete();
        var coordinated = barrier.SignalAndWait(TimeSpan.FromSeconds(10));
        var results = await Task.WhenAll(first, second);

        Assert.True(coordinated);
        var created = Assert.Single(results.OfType<ReservationResult.Created>());
        var rejected = Assert.Single(results.OfType<ReservationResult.Rejected>());
        Assert.Equal(ReservationError.Overlap, rejected.Error);
        Assert.Equal(created.Reservation, Assert.Single(service.GetSnapshot()));
    }

    [Fact]
    public void SnapshotDoesNotExposeMutableStorage()
    {
        var service = CreateService();
        var created = Assert.IsType<ReservationResult.Created>(service.Reserve("A", At(10), At(11)));
        var snapshot = service.GetSnapshot();
        snapshot[0] = snapshot[0] with { RoomId = "B" };

        Assert.Equal(created.Reservation, Assert.Single(service.GetSnapshot()));
    }

    [Fact]
    public async Task HostRegistersOneServiceSharedAcrossScopes()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();

        Assert.Same(first.ServiceProvider.GetRequiredService<ReservationService>(),
            second.ServiceProvider.GetRequiredService<ReservationService>());
        Assert.Same(TimeProvider.System, factory.Services.GetRequiredService<TimeProvider>());
    }

    private static ReservationService CreateService() => new(new ControlledTimeProvider(Now));
    private static DateTimeOffset At(int hour, int minute = 0) => new(2030, 6, 1, hour, minute, 0, TimeSpan.Zero);

    private sealed class ControlledTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = now;
        private int readCount;
        public int ReadCount => Volatile.Read(ref readCount);

        public override DateTimeOffset GetUtcNow()
        {
            Interlocked.Increment(ref readCount);
            return UtcNow;
        }
    }
}
