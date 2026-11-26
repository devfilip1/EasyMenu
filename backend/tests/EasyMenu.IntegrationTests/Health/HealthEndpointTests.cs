using System.Net;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Health;

public sealed class HealthEndpointTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Get_health_returns_ok_when_database_is_reachable()
    {
        var response = await Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthBody>();
        body!.Status.Should().Be("healthy");
    }

    private sealed record HealthBody(string Status);
}
