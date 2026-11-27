using System.Net;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace EasyMenu.IntegrationTests.Realtime;

/// <summary>
/// Hub <c>/hubs/orders</c> (contracts/realtime.md, FR-029..FR-031a): anonymous; <c>SubscribeToOrder(publicToken)</c>
/// joins the order's group; <c>OrderUpdated</c> (PublicOrder) only when the ORDER status changes.
/// </summary>
public sealed class OrdersHubTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(2);

    private readonly List<HubConnection> _connections = [];

    public override async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private async Task<HubConnection> ConnectAsync()
    {
        var connection = await Factory.ConnectOrdersHubAsync();
        _connections.Add(connection);
        return connection;
    }

    private static Task<bool> SubscribeAsync(HubConnection connection, string publicToken) =>
        connection.InvokeAsync<bool>("SubscribeToOrder", publicToken).WaitAsync(HubTestHelpers.DefaultTimeout);

    private async Task<(string KitchenToken, PublicOrderDto Public, KitchenOrderDto Kitchen)> PlaceMixedOrderAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var kitchenToken = await Api.CreateKitchenTokenAsync(owner.EstablishmentId);
        var (placed, kitchen) = await Api.PlaceKitchenOrderAsync(owner, "Maria",
            new PlaceOrderLine(menu.Caipirinha.Id, 1), new PlaceOrderLine(menu.Batata.Id, 1));
        return (kitchenToken, placed, kitchen);
    }

    private async Task MoveAsync(string kitchenToken, Guid orderId, string station, string status) =>
        (await Api.PutPartStatusAsync(kitchenToken, orderId, station, status)).StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task Anonymous_connection_is_allowed()
    {
        (await ConnectAsync()).State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task Subscribe_returns_true_for_a_valid_token_and_false_for_an_invented_one()
    {
        var (_, placed, _) = await PlaceMixedOrderAsync();
        var connection = await ConnectAsync();

        (await SubscribeAsync(connection, placed.PublicToken)).Should().BeTrue();
        (await SubscribeAsync(connection, "inventado")).Should().BeFalse();
        (await SubscribeAsync(connection, new string('A', 43))).Should().BeFalse();
        (await SubscribeAsync(connection, placed.PickupCode)).Should().BeFalse("the short pickup code is not an identifier");
    }

    [Fact]
    public async Task Order_updated_is_sent_for_in_preparation_ready_and_picked_up_only()
    {
        var (kitchenToken, placed, order) = await PlaceMixedOrderAsync();
        var connection = await ConnectAsync();
        (await SubscribeAsync(connection, placed.PublicToken)).Should().BeTrue();
        var received = HubTestHelpers.RecordPayloads<PublicOrderDto>(connection, "OrderUpdated");

        await MoveAsync(kitchenToken, order.Id, "bar", "in_preparation");      // paid → in_preparation
        await MoveAsync(kitchenToken, order.Id, "kitchen", "in_preparation");  // order status unchanged
        await MoveAsync(kitchenToken, order.Id, "bar", "ready");               // still in_preparation
        await MoveAsync(kitchenToken, order.Id, "kitchen", "ready");           // → ready
        (await Api.PutPickupAsync(kitchenToken, order)).StatusCode.Should().Be(HttpStatusCode.OK); // → picked_up

        await WaitUntilAsync(() => received.Any(e => e.Status == "picked_up"));
        await Task.Delay(TimeSpan.FromMilliseconds(500)); // let any extra (wrong) event arrive

        received.Select(e => e.Status).Should().Equal("in_preparation", "ready", "picked_up");
        received.Should().OnlyContain(e => e.PublicToken == placed.PublicToken && e.PickupCode == placed.PickupCode);
        received.Last().TotalCents.Should().Be(placed.TotalCents);
        received.Last().Items.Should().BeEquivalentTo(placed.Items, "events carry the full PublicOrder snapshot");
    }

    [Fact]
    public async Task No_event_when_the_second_part_starts_and_the_order_status_does_not_change()
    {
        var (kitchenToken, placed, order) = await PlaceMixedOrderAsync();
        await MoveAsync(kitchenToken, order.Id, "bar", "in_preparation");
        var connection = await ConnectAsync();
        (await SubscribeAsync(connection, placed.PublicToken)).Should().BeTrue();
        var received = HubTestHelpers.RecordPayloads<PublicOrderDto>(connection, "OrderUpdated");

        await MoveAsync(kitchenToken, order.Id, "kitchen", "in_preparation");
        await Task.Delay(Quiet);

        received.Should().BeEmpty("the order stayed in_preparation");
    }

    [Fact]
    public async Task Subscriber_of_order_x_does_not_receive_events_of_order_y()
    {
        var (_, orderX, _) = await PlaceMixedOrderAsync();
        var (kitchenY, orderY, kitchenOrderY) = await PlaceMixedOrderAsync();
        var subscriberX = await ConnectAsync();
        var subscriberY = await ConnectAsync();
        (await SubscribeAsync(subscriberX, orderX.PublicToken)).Should().BeTrue();
        (await SubscribeAsync(subscriberY, orderY.PublicToken)).Should().BeTrue();
        var receivedByX = HubTestHelpers.RecordPayloads<PublicOrderDto>(subscriberX, "OrderUpdated");
        var receivedByY = HubTestHelpers.WaitForEventAsync<PublicOrderDto>(subscriberY, "OrderUpdated");

        await MoveAsync(kitchenY, kitchenOrderY.Id, "bar", "in_preparation");

        (await receivedByY).PublicToken.Should().Be(orderY.PublicToken, "sanity: events are flowing");
        await Task.Delay(Quiet);
        receivedByX.Should().BeEmpty();
    }

    [Fact]
    public async Task Unsubscribed_connection_receives_nothing()
    {
        var (kitchenToken, _, order) = await PlaceMixedOrderAsync();
        var connection = await ConnectAsync();
        var received = HubTestHelpers.RecordPayloads<PublicOrderDto>(connection, "OrderUpdated");

        await MoveAsync(kitchenToken, order.Id, "bar", "in_preparation");
        await Task.Delay(Quiet);

        received.Should().BeEmpty();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + HubTestHelpers.DefaultTimeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }
}
