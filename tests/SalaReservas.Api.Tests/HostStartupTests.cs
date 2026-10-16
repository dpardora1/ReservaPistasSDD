using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SalaReservas.Api.Tests;

public class HostStartupTests
{
    [Fact]
    public async Task HostStartsAndRespondsToHttpRequests()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
