using System.Net;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EasyMenu.IntegrationTests.RateLimiting;

/// <summary>
/// Rate limiting of the auth endpoints (research R14, policy <c>auth</c>): fixed window per IP and per endpoint.
/// Uses a dedicated host with <c>PermitLimit = 2</c>, so the 3rd call in the window is rejected.
/// </summary>
public sealed class AuthRateLimitTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const int PermitLimit = 2;

    private WebApplicationFactory<Program>? _limitedHost;
    private HttpClient? _limitedClient;

    private HttpClient Limited => _limitedClient ??= (_limitedHost = Factory.WithRateLimits(PermitLimit)).CreateClient();

    public override async Task DisposeAsync()
    {
        _limitedClient?.Dispose();
        if (_limitedHost is not null)
        {
            await _limitedHost.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private Task<HttpResponseMessage> LoginAsync() =>
        Limited.PostAsJsonAsync("/api/auth/login", new LoginRequest("ninguem@bar.test", "SenhaErrada!1"), ApiDriver.Json);

    private Task<HttpResponseMessage> RegisterAsync() =>
        Limited.PostAsJsonAsync("/api/auth/register-owner", new RegisterOwnerRequest(
            Name: null,
            Email: $"dono-{Guid.NewGuid():N}@bar.test",
            Password: "Senha@1234",
            EstablishmentName: "Bar",
            Slug: $"bar-{Guid.NewGuid():N}"[..20]), ApiDriver.Json);

    private static async Task AssertRateLimitedAsync(HttpResponseMessage response)
    {
        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(429, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("rate_limited");
        problem.BodyStatus.Should().Be(429);

        var retryAfter = response.Headers.RetryAfter;
        retryAfter.Should().NotBeNull("a 429 must tell the client when to retry");
        retryAfter!.Delta.Should().NotBeNull("Retry-After must be in seconds");
        retryAfter.Delta!.Value.Should().BePositive();
    }

    [Fact]
    public async Task Third_login_in_the_window_returns_429_rate_limited()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            (await LoginAsync()).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, "call {0} is within the limit", i + 1);
        }

        await AssertRateLimitedAsync(await LoginAsync());
    }

    [Fact]
    public async Task Third_register_in_the_window_returns_429_rate_limited()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            (await RegisterAsync()).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, "call {0} is within the limit", i + 1);
        }

        await AssertRateLimitedAsync(await RegisterAsync());
    }

    [Fact]
    public async Task Exhausting_login_does_not_block_register()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            await LoginAsync();
        }

        await AssertRateLimitedAsync(await LoginAsync());

        // The auth policy is partitioned per endpoint: register still has its own budget.
        var register = await RegisterAsync();
        register.StatusCode.Should().Be(HttpStatusCode.Created, await register.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_is_never_rate_limited()
    {
        for (var i = 0; i < PermitLimit * 5; i++)
        {
            (await Limited.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK, "call {0}", i + 1);
        }
    }
}
