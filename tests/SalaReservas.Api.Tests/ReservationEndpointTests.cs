using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SalaReservas.Api;

namespace SalaReservas.Api.Tests;

public class ReservationEndpointTests
{
    [Fact]
    public async Task ValidRequestReturns201WithStoredReservationAndUtcDates()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/reservations", new
        {
            roomId = "A", start = "2030-06-01T12:00:00+02:00", end = "2030-06-01T13:00:00+02:00"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<Reservation>();
        Assert.NotNull(reservation);
        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal("A", reservation.RoomId);
        Assert.Equal(new DateTimeOffset(2030, 6, 1, 10, 0, 0, TimeSpan.Zero), reservation.Start);
        Assert.Equal(reservation.Start.AddHours(1), reservation.End);
        Assert.Equal(TimeSpan.Zero, reservation.Start.Offset);
        Assert.Equal(TimeSpan.Zero, reservation.End.Offset);
        Assert.Equal(reservation, Assert.Single(factory.Services.GetRequiredService<ReservationService>().GetSnapshot()));
    }

    [Theory]
    [InlineData("", "invalid_request")]
    [InlineData("{", "invalid_request")]
    [InlineData("null", "invalid_request")]
    [InlineData("[]", "invalid_request")]
    [InlineData("{}", "invalid_request")]
    [InlineData("{\"roomId\":\"A\",\"start\":\"2030-06-01T10:00:00Z\"}", "invalid_request")]
    [InlineData("{\"roomId\":\"A\",\"end\":\"2030-06-01T11:00:00Z\"}", "invalid_request")]
    [InlineData("{\"start\":\"2030-06-01T10:00:00Z\",\"end\":\"2030-06-01T11:00:00Z\"}", "invalid_request")]
    [InlineData("{\"roomId\":null,\"start\":\"2030-06-01T10:00:00Z\",\"end\":\"2030-06-01T11:00:00Z\"}", "invalid_request")]
    [InlineData("{\"roomId\":\"A\",\"start\":null,\"end\":\"2030-06-01T11:00:00Z\"}", "invalid_request")]
    [InlineData("{\"roomId\":\"A\",\"start\":\"2030-06-01T10:00:00Z\",\"end\":null}", "invalid_request")]
    [InlineData("{\"roomId\":12,\"start\":\"2030-06-01T10:00:00Z\",\"end\":\"2030-06-01T11:00:00Z\"}", "invalid_request")]
    [InlineData("{\"roomId\":\"A\",\"start\":12,\"end\":\"2030-06-01T11:00:00Z\"}", "invalid_request")]
    [InlineData("{\"roomId\":\"A\",\"start\":\"2030-06-01T10:00:00Z\",\"end\":{}}", "invalid_request")]
    public async Task MalformedOrIncompleteJsonReturnsProblemDetailsWithoutMutation(string json, string code)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var service = factory.Services.GetRequiredService<ReservationService>();
        Seed(service);
        var before = service.GetSnapshot();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/reservations", content);

        await AssertProblem(response, HttpStatusCode.BadRequest, code);
        Assert.Equal(before, service.GetSnapshot());
    }

    [Theory]
    [InlineData("", "2030-06-01T10:00:00Z", "2030-06-01T11:00:00Z", 400, "invalid_request")]
    [InlineData("A", "", "2030-06-01T11:00:00Z", 400, "invalid_request")]
    [InlineData("A", "2030-06-01T10:00:00Z", " ", 400, "invalid_request")]
    [InlineData("A", "2030-06-01T10:00:00", "2030-06-01T11:00:00Z", 400, "invalid_instant")]
    [InlineData("A", "2030-06-01T10:00:00Z", "2030-06-01T11:00:00", 400, "invalid_instant")]
    [InlineData("A", "2030-02-30T10:00:00Z", "2030-06-01T11:00:00Z", 400, "invalid_instant")]
    [InlineData("A", "2030-06-01T10:00:00Z", "2030-06-01T10:00:00Z", 400, "invalid_interval")]
    [InlineData("A", "2030-06-01T11:00:00Z", "2030-06-01T10:00:00Z", 400, "invalid_interval")]
    [InlineData("A", "2030-06-01T07:00:00Z", "2030-06-01T10:00:00Z", 400, "start_in_past")]
    [InlineData("missing", "2030-06-01T10:00:00Z", "2030-06-01T11:00:00Z", 404, "room_not_found")]
    [InlineData("B", "2030-06-01T14:30:00Z", "2030-06-01T15:30:00Z", 409, "reservation_overlap")]
    public async Task RejectionsMapToStableHttpErrorsAndPreserveState(
        string roomId, string start, string end, int status, string code)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var service = factory.Services.GetRequiredService<ReservationService>();
        Seed(service);
        var before = service.GetSnapshot();

        using var response = await client.PostAsJsonAsync("/reservations", new { roomId, start, end });

        await AssertProblem(response, (HttpStatusCode)status, code);
        Assert.Equal(before, service.GetSnapshot());
    }

    [Fact]
    public async Task NonJsonContentReturnsInvalidRequest()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("not json");

        using var response = await client.PostAsync("/reservations", content);

        await AssertProblem(response, HttpStatusCode.BadRequest, "invalid_request");
        Assert.Empty(factory.Services.GetRequiredService<ReservationService>().GetSnapshot());
    }

    [Fact]
    public async Task RepeatedRequestsShareStateAndReturnConflict()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = new { roomId = "A", start = "2030-06-01T10:00:00Z", end = "2030-06-01T11:00:00Z" };

        using var first = await client.PostAsJsonAsync("/reservations", request);
        using var second = await client.PostAsJsonAsync("/reservations", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await AssertProblem(second, HttpStatusCode.Conflict, "reservation_overlap");
        Assert.Single(factory.Services.GetRequiredService<ReservationService>().GetSnapshot());
    }

    [Theory]
    [InlineData("A02", "A", "2030-06-01T10:30:00Z", "2030-06-01T11:30:00Z", false)]
    [InlineData("A03", "A", "2030-06-01T11:00:00Z", "2030-06-01T12:00:00Z", true)]
    [InlineData("A03-before", "A", "2030-06-01T09:00:00Z", "2030-06-01T10:00:00Z", true)]
    [InlineData("A04", "A", "2030-06-01T09:00:00Z", "2030-06-01T12:00:00Z", false)]
    [InlineData("contained", "A", "2030-06-01T10:15:00Z", "2030-06-01T10:45:00Z", false)]
    [InlineData("identical", "A", "2030-06-01T10:00:00Z", "2030-06-01T11:00:00Z", false)]
    [InlineData("offset-equivalent", "A", "2030-06-01T12:00:00+02:00", "2030-06-01T13:00:00+02:00", false)]
    [InlineData("A05", "B", "2030-06-01T10:00:00Z", "2030-06-01T11:00:00Z", true)]
    public async Task R05_R06_IntervalAcceptanceThroughHttp(
        string scenario, string roomId, string start, string end, bool accepted)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var first = await client.PostAsJsonAsync("/reservations", new
        {
            roomId = "A", start = "2030-06-01T10:00:00Z", end = "2030-06-01T11:00:00Z"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var service = factory.Services.GetRequiredService<ReservationService>();
        var before = service.GetSnapshot();

        using var response = await client.PostAsJsonAsync("/reservations", new { roomId, start, end });

        if (accepted)
        {
            Assert.True(response.StatusCode == HttpStatusCode.Created, scenario);
            var created = await response.Content.ReadFromJsonAsync<Reservation>();
            Assert.NotNull(created);
            Assert.NotEqual(before[0].Id, created.Id);
            Assert.Equal(new[] { before[0], created }, service.GetSnapshot());
        }
        else
        {
            await AssertProblem(response, HttpStatusCode.Conflict, "reservation_overlap");
            Assert.Equal(before, service.GetSnapshot());
        }
    }

    [Theory]
    [InlineData("2030-06-01T08:00:00Z", "2030-06-01T08:00:00.0000001Z", true)]
    [InlineData("2030-06-01T07:59:59.9999999Z", "2030-06-01T09:00:00Z", false)]
    [InlineData("2030-06-01T08:00:00Z", "2080-06-01T08:00:00Z", true)]
    [InlineData("2130-06-01T08:00:00Z", "9999-12-31T23:59:59.9999999Z", true)]
    public async Task R03_R04_TimeBoundariesThroughHttp(string start, string end, bool accepted)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/reservations", new { roomId = "A", start, end });

        var state = factory.Services.GetRequiredService<ReservationService>().GetSnapshot();
        if (accepted)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var reservation = await response.Content.ReadFromJsonAsync<Reservation>();
            Assert.NotNull(reservation);
            Assert.Equal(DateTimeOffset.Parse(start, System.Globalization.CultureInfo.InvariantCulture), reservation.Start);
            Assert.Equal(DateTimeOffset.Parse(end, System.Globalization.CultureInfo.InvariantCulture), reservation.End);
            Assert.Equal(reservation, Assert.Single(state));
        }
        else
        {
            await AssertProblem(response, HttpStatusCode.BadRequest, "start_in_past");
            Assert.Empty(state);
        }
    }

    [Fact]
    public async Task R07_A10_ConcurrentHttpRequestsCreateExactlyOneReservation()
    {
        await using var factory = CreateFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new { roomId = "A", start = "2030-06-01T10:00:00Z", end = "2030-06-01T11:00:00Z" };

        async Task<HttpResponseMessage> Send(HttpClient client)
        {
            await gate.Task;
            return await client.PostAsJsonAsync("/reservations", request);
        }

        var first = Send(firstClient);
        var second = Send(secondClient);
        gate.SetResult();
        var responses = await Task.WhenAll(first, second);
        try
        {
            var success = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            var conflict = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            await AssertProblem(conflict, HttpStatusCode.Conflict, "reservation_overlap");
            var created = await success.Content.ReadFromJsonAsync<Reservation>();
            Assert.NotNull(created);
            Assert.Equal(created, Assert.Single(factory.Services.GetRequiredService<ReservationService>().GetSnapshot()));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider());
        }));

    private static void Seed(ReservationService service) =>
        Assert.IsType<ReservationResult.Created>(service.Reserve("B",
            new DateTimeOffset(2030, 6, 1, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2030, 6, 1, 15, 0, 0, TimeSpan.Zero)));

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("detail").GetString()));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 6, 1, 8, 0, 0, TimeSpan.Zero);
    }
}
