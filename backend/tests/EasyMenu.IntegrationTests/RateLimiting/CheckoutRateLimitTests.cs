using System.Net;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EasyMenu.IntegrationTests.RateLimiting;

/// <summary>
/// Rate limiting of the checkout (research R14, policy <c>checkout</c>). The limited host shares the database,
/// so the scenario is built through the regular (unlimited) host.
/// </summary>
public sealed class CheckoutRateLimitTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
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

    private async Task<string> ArrangeSlugAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        await Api.CreateMenuAsync(owner.Token);
        return owner.Response.User.Establishment.Slug;
    }

    [Fact]
    public async Task Third_checkout_in_the_window_returns_429_even_after_rejected_attempts()
    {
        var slug = await ArrangeSlugAsync();
        var emptyCart = new PlaceOrderRequest(null, []);

        for (var i = 0; i < PermitLimit; i++)
        {
            var problem = await ApiDriver.ReadProblemAsync(await Api.PostOrderAsync(slug, emptyCart, Limited));
            problem.Status.Should().Be(400, "call {0} is within the limit: {1}", i + 1, problem.RawBody);
            problem.Code.Should().Be("empty_cart");
        }

        var response = await Api.PostOrderAsync(slug, emptyCart, Limited);

        var limited = await ApiDriver.ReadProblemAsync(response);
        limited.Status.Should().Be(429, limited.RawBody);
        limited.MediaType.Should().Be("application/problem+json");
        limited.Code.Should().Be("rate_limited");
        response.Headers.RetryAfter.Should().NotBeNull();
        response.Headers.RetryAfter!.Delta.Should().NotBeNull();
        response.Headers.RetryAfter.Delta!.Value.Should().BePositive();
    }

    [Fact]
    public async Task Public_menu_is_not_rate_limited()
    {
        var slug = await ArrangeSlugAsync();

        for (var i = 0; i < PermitLimit * 5; i++)
        {
            (await Api.GetPublicMenuResponseAsync(slug, Limited)).StatusCode.Should().Be(HttpStatusCode.OK, "call {0}", i + 1);
        }
    }

    [Fact]
    public async Task Exhausting_checkout_does_not_block_the_public_menu()
    {
        var slug = await ArrangeSlugAsync();
        for (var i = 0; i <= PermitLimit; i++)
        {
            await Api.PostOrderAsync(slug, new PlaceOrderRequest(null, []), Limited);
        }

        (await Api.GetPublicMenuResponseAsync(slug, Limited)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
