using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace EasyMenu.IntegrationTests.RateLimiting;

/// <summary>
/// Rate limit of <c>SubscribeToOrder</c> on <c>/hubs/orders</c> (research R14, policy OrderSubscribe): the hub
/// method fails with <see cref="HubException"/> "rate_limited" and the connection stays open.
/// </summary>
public sealed class OrderSubscribeRateLimitTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const int PermitLimit = 2;

    private WebApplicationFactory<Program>? _limitedHost;
    private HubConnection? _connection;

    private WebApplicationFactory<Program> LimitedHost => _limitedHost ??= Factory.WithRateLimits(PermitLimit);

    public override async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        if (_limitedHost is not null)
        {
            await _limitedHost.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private async Task<HubConnection> ConnectAsync() => _connection = await LimitedHost.ConnectOrdersHubAsync();

    private static Task<bool> SubscribeAsync(HubConnection connection, string publicToken) =>
        connection.InvokeAsync<bool>("SubscribeToOrder", publicToken).WaitAsync(HubTestHelpers.DefaultTimeout);

    private async Task<(string KitchenToken, PublicOrderDto Public, KitchenOrderDto Kitchen)> PlaceOrderAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var kitchenToken = await Api.CreateKitchenTokenAsync(owner.EstablishmentId);
        var (placed, kitchen) = await Api.PlaceKitchenOrderAsync(owner, null,
            new PlaceOrderLine(menu.Caipirinha.Id, 1), new PlaceOrderLine(menu.Batata.Id, 1));
        return (kitchenToken, placed, kitchen);
    }

    [Fact]
    public async Task Third_subscribe_in_the_window_fails_with_rate_limited_and_keeps_the_connection()
    {
        var (_, placed, _) = await PlaceOrderAsync();
        var connection = await ConnectAsync();

        for (var i = 0; i < PermitLimit; i++)
        {
            (await SubscribeAsync(connection, placed.PublicToken)).Should().BeTrue("call {0} is within the limit", i + 1);
        }

        var act = () => SubscribeAsync(connection, placed.PublicToken);

        (await act.Should().ThrowAsync<HubException>()).Which.Message.Should().EndWith("rate_limited");
        connection.State.Should().Be(HubConnectionState.Connected, "a rejected invocation does not close the connection");
    }

    [Fact]
    public async Task Rate_limit_message_is_exactly_rate_limited()
    {
        var (_, placed, _) = await PlaceOrderAsync();
        var connection = await ConnectAsync();
        for (var i = 0; i < PermitLimit; i++)
        {
            await SubscribeAsync(connection, placed.PublicToken);
        }

        var exception = await Record.ExceptionAsync(() => SubscribeAsync(connection, placed.PublicToken));

        // The client prefixes server HubException messages with "An unexpected error occurred invoking '...' on the server. HubException: ".
        exception.Should().BeOfType<HubException>();
        exception!.Message.Split("HubException: ").Last().Should().Be("rate_limited");
    }

    [Fact]
    public async Task Events_of_already_subscribed_orders_keep_arriving_after_being_limited()
    {
        var (kitchenToken, placed, order) = await PlaceOrderAsync();
        var connection = await ConnectAsync();
        (await SubscribeAsync(connection, placed.PublicToken)).Should().BeTrue();
        await SubscribeAsync(connection, placed.PublicToken);
        await Record.ExceptionAsync(() => SubscribeAsync(connection, placed.PublicToken)); // limited
        var updated = HubTestHelpers.WaitForEventAsync<PublicOrderDto>(connection, "OrderUpdated");

        // The status change must go through the same host as the hub connection to reach its groups.
        using var kitchen = LimitedHost.CreateClient();
        kitchen.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kitchenToken);
        (await kitchen.PutAsJsonAsync($"/api/kitchen/orders/{order.Id}/parts/bar/status",
            new UpdatePartStatusRequest("in_preparation"), ApiDriver.Json)).StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await updated;
        payload.PublicToken.Should().Be(placed.PublicToken);
        payload.Status.Should().Be("in_preparation");
        connection.State.Should().Be(HubConnectionState.Connected);
    }
}
