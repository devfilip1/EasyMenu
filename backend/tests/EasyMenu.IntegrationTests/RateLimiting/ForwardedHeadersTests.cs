using System.Net;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMenu.IntegrationTests.RateLimiting;

/// <summary>
/// X-Forwarded-For handling (research R14/R18): behind a trusted reverse proxy the rate limit partitions by the
/// real client IP; without a configured (or with a non-matching) proxy, X-Forwarded-For is ignored so it cannot
/// be spoofed to dodge the limit. The TCP peer is fixed to 127.0.0.1 by <see cref="FixedRemoteIpStartupFilter"/>.
/// </summary>
public sealed class ForwardedHeadersTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const int PermitLimit = 2;

    private readonly List<IAsyncDisposable> _hosts = [];

    public override async Task DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    /// <summary>Low-limit host whose TCP peer is 127.0.0.1, with the given ForwardedHeaders settings.</summary>
    private HttpClient Host(params (string Key, string Value)[] settings)
    {
        var host = Factory.WithRateLimits(PermitLimit).WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
                services.AddTransient<IStartupFilter>(_ => new FixedRemoteIpStartupFilter(IPAddress.Loopback)));
        });
        _hosts.Add(host);
        return host.CreateClient();
    }

    private static async Task<HttpResponseMessage> LoginFromAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest("ninguem@bar.test", "SenhaErrada!1"), options: ApiDriver.Json),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }

    private static async Task AssertRateLimitedAsync(HttpResponseMessage response)
    {
        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(429, problem.RawBody);
        problem.Code.Should().Be("rate_limited");
    }

    /// <summary>Trusted proxy: the 3rd login of one client is limited, another client behind the same proxy is not.</summary>
    private static async Task AssertPartitionedByForwardedClientAsync(HttpClient client)
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            (await LoginFromAsync(client, "203.0.113.1")).StatusCode
                .Should().NotBe(HttpStatusCode.TooManyRequests, "call {0} of 203.0.113.1 is within the limit", i + 1);
        }

        await AssertRateLimitedAsync(await LoginFromAsync(client, "203.0.113.1"));

        (await LoginFromAsync(client, "203.0.113.2")).StatusCode
            .Should().NotBe(HttpStatusCode.TooManyRequests, "203.0.113.2 is another real client with its own budget");
    }

    /// <summary>Untrusted X-Forwarded-For: varying it does not create new budgets (all calls share the peer's).</summary>
    private static async Task AssertForwardedForIsIgnoredAsync(HttpClient client)
    {
        (await LoginFromAsync(client, "203.0.113.1")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        (await LoginFromAsync(client, "203.0.113.2")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        await AssertRateLimitedAsync(await LoginFromAsync(client, "203.0.113.3"));
    }

    [Fact]
    public async Task Known_proxy_makes_the_rate_limit_partition_by_the_real_client_ip()
    {
        var client = Host(("ForwardedHeaders:KnownProxies:0", "127.0.0.1"));

        await AssertPartitionedByForwardedClientAsync(client);
    }

    [Fact]
    public async Task Known_network_containing_the_proxy_is_trusted_too()
    {
        var client = Host(("ForwardedHeaders:KnownNetworks:0", "127.0.0.0/8"));

        await AssertPartitionedByForwardedClientAsync(client);
    }

    [Fact]
    public async Task Without_configured_proxy_x_forwarded_for_is_ignored_and_cannot_be_spoofed()
    {
        // Default settings: empty lists mean "trust nobody" (ASP.NET's built-in loopback defaults must be cleared).
        var client = Host();

        await AssertForwardedForIsIgnoredAsync(client);
    }

    [Fact]
    public async Task Untrusted_proxy_x_forwarded_for_is_ignored()
    {
        var client = Host(("ForwardedHeaders:KnownProxies:0", "10.0.0.1"));

        await AssertForwardedForIsIgnoredAsync(client);
    }
}
