using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SalaReservas.Api;

namespace SalaReservas.Api.Tests;

public class CancellationEndpointTests
{
    private static readonly DateTimeOffset Now = new(2030, 6, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task C02_B01_DeleteReturns204AndPreservesReservationData()
    {
        await using var factory = CreateFactory(new Clock());
        using var client = factory.CreateClient();
        var reservation = await Create(client);

        using var response = await client.DeleteAsync($"/reservations/{reservation.Id}");

        await AssertNoContent(response);
        Assert.Equal(reservation with { IsCancelled = true },
            Assert.Single(factory.Services.GetRequiredService<ReservationService>().GetSnapshot()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task C04_B04_RepeatedDeleteReturns204EvenAfterStart(int advanceHours)
    {
        var clock = new Clock();
        await using var factory = CreateFactory(clock);
        using var client = factory.CreateClient();
        var reservation = await Create(client);
        using var first = await client.DeleteAsync($"/reservations/{reservation.Id}");
        await AssertNoContent(first);
        var service = factory.Services.GetRequiredService<ReservationService>();
        var before = service.GetSnapshot();
        clock.UtcNow = Now.AddHours(advanceHours);

        using var repeat = await client.DeleteAsync($"/reservations/{reservation.Id}");

        await AssertNoContent(repeat);
        Assert.Equal(before, service.GetSnapshot());
    }

    [Theory]
    [InlineData("not-a-guid", 400, "invalid_reservation_id")]
    [InlineData("123", 400, "invalid_reservation_id")]
    [InlineData("00000000-0000-0000-0000-00000000000g", 400, "invalid_reservation_id")]
    [InlineData("00000000-0000-0000-0000-000000000000", 404, "reservation_not_found")]
    [InlineData("8a40a68c-9fbb-45c6-8140-6dcb0b684512", 404, "reservation_not_found")]
    public async Task InvalidOrUnknownIdReturnsProblemWithoutChangingState(string id, int status, string code)
    {
        await using var factory = CreateFactory(new Clock());
        using var client = factory.CreateClient();
        await Create(client);
        var service = factory.Services.GetRequiredService<ReservationService>();
        var before = service.GetSnapshot();

        using var response = await client.DeleteAsync($"/reservations/{id}");

        await AssertProblem(response, (HttpStatusCode)status, code);
        Assert.Equal(before, service.GetSnapshot());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(72000000000)]
    public async Task C05_B05_StartedReservationReturns409WithoutChangingState(long ticksAfterStart)
    {
        var clock = new Clock();
        await using var factory = CreateFactory(clock);
        using var client = factory.CreateClient();
        var reservation = await Create(client);
        var service = factory.Services.GetRequiredService<ReservationService>();
        var before = service.GetSnapshot();
        clock.UtcNow = reservation.Start.AddTicks(ticksAfterStart);

        using var response = await client.DeleteAsync($"/reservations/{reservation.Id}");

        await AssertProblem(response, HttpStatusCode.Conflict, "reservation_started");
        Assert.Equal(before, service.GetSnapshot());
    }

    [Fact]
    public async Task C05_OneTickBeforeStartReturns204()
    {
        var clock = new Clock();
        await using var factory = CreateFactory(clock);
        using var client = factory.CreateClient();
        var reservation = await Create(client);
        clock.UtcNow = reservation.Start.AddTicks(-1);

        using var response = await client.DeleteAsync($"/reservations/{reservation.Id}");

        await AssertNoContent(response);
        Assert.True(Assert.Single(factory.Services.GetRequiredService<ReservationService>().GetSnapshot()).IsCancelled);
    }

    [Fact]
    public async Task C03_B02_CancelAllowsReplacementAndOldIdCannotCancelIt()
    {
        await using var factory = CreateFactory(new Clock());
        using var client = factory.CreateClient();
        var original = await Create(client);
        using var cancellation = await client.DeleteAsync($"/reservations/{original.Id}");
        await AssertNoContent(cancellation);
        var replacement = await Create(client);
        Assert.NotEqual(original.Id, replacement.Id);
        var service = factory.Services.GetRequiredService<ReservationService>();
        var before = service.GetSnapshot();

        using var repeat = await client.DeleteAsync($"/reservations/{original.Id}");

        await AssertNoContent(repeat);
        Assert.Equal(before, service.GetSnapshot());
        Assert.Equal(new[] { original with { IsCancelled = true }, replacement }, service.GetSnapshot());
    }

    [Fact]
    public async Task C06_B06_ConcurrentCreateAndCancelRespectEitherSerialOrder()
    {
        await using var factory = CreateFactory(new Clock());
        using var creator = factory.CreateClient();
        using var canceller = factory.CreateClient();
        var original = await Create(creator);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<HttpResponseMessage> Cancel()
        {
            await gate.Task;
            return await canceller.DeleteAsync($"/reservations/{original.Id}");
        }

        async Task<HttpResponseMessage> Reserve()
        {
            await gate.Task;
            return await creator.PostAsJsonAsync("/reservations", new
            {
                roomId = "A", start = "2030-06-01T10:00:00Z", end = "2030-06-01T11:00:00Z"
            });
        }

        var cancelTask = Cancel();
        var createTask = Reserve();
        gate.SetResult();
        await Task.WhenAll(cancelTask, createTask);
        using var cancelled = await cancelTask;
        using var created = await createTask;
        await AssertNoContent(cancelled);
        var service = factory.Services.GetRequiredService<ReservationService>();
        var state = service.GetSnapshot();
        Assert.Equal(original with { IsCancelled = true }, Assert.Single(state, r => r.Id == original.Id));

        if (created.StatusCode == HttpStatusCode.Created)
        {
            var replacement = await created.Content.ReadFromJsonAsync<Reservation>();
            Assert.NotNull(replacement);
            Assert.NotEqual(original.Id, replacement.Id);
            Assert.Equal(replacement, Assert.Single(state, r => !r.IsCancelled));
            Assert.Equal(2, state.Length);
        }
        else
        {
            await AssertProblem(created, HttpStatusCode.Conflict, "reservation_overlap");
            Assert.Single(state);
            var replacement = await Create(creator);
            Assert.NotEqual(original.Id, replacement.Id);
            Assert.Equal(replacement, Assert.Single(service.GetSnapshot(), r => !r.IsCancelled));
        }
    }

    [Fact]
    public async Task C04_ConcurrentDeletesBothSucceedWithOneStateTransition()
    {
        await using var factory = CreateFactory(new Clock());
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var original = await Create(firstClient);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Send(HttpClient client)
        {
            await gate.Task;
            return await client.DeleteAsync($"/reservations/{original.Id}");
        }

        var first = Send(firstClient);
        var second = Send(secondClient);
        gate.SetResult();
        await Task.WhenAll(first, second);
        using var firstResponse = await first;
        using var secondResponse = await second;
        await AssertNoContent(firstResponse);
        await AssertNoContent(secondResponse);
        Assert.Equal(original with { IsCancelled = true },
            Assert.Single(factory.Services.GetRequiredService<ReservationService>().GetSnapshot()));
    }

    private static WebApplicationFactory<Program> CreateFactory(Clock clock) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));

    private static async Task<Reservation> Create(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/reservations", new
        {
            roomId = "A", start = "2030-06-01T10:00:00Z", end = "2030-06-01T11:00:00Z"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<Reservation>();
        Assert.NotNull(reservation);
        return reservation;
    }

    private static async Task AssertNoContent(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("detail").GetString()));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = Now;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
