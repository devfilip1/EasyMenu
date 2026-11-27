using System.Net;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace EasyMenu.IntegrationTests.Realtime;

/// <summary>
/// Hub <c>/hubs/kitchen</c> (contracts/realtime.md): authenticated (KitchenAccess), one group per establishment
/// taken from the token claim; events OrderCreated, OrderUpdated and OrderPickedUp; tenant isolated.
/// </summary>
public sealed class KitchenHubTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly string[] AllEvents = ["OrderCreated", "OrderUpdated", "OrderPickedUp"];

    private readonly List<HubConnection> _connections = [];

    public override async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private async Task<HubConnection> ConnectAsync(string token)
    {
        var connection = await Factory.ConnectKitchenHubAsync(token);
        _connections.Add(connection);
        return connection;
    }

    private async Task<(RegisteredOwner Owner, DefaultMenu Menu, string KitchenToken)> ArrangeAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        return (owner, menu, await Api.CreateKitchenTokenAsync(owner.EstablishmentId));
    }

    private Task<PublicOrderDto> PlaceMixedOrderAsync(RegisteredOwner owner, DefaultMenu menu) =>
        Api.PlaceOrderAsync(owner.Response.User.Establishment.Slug, "Maria",
            new PlaceOrderLine(menu.Caipirinha.Id, 2), new PlaceOrderLine(menu.Batata.Id, 1));

    // ------------------------------------------------------------------ authentication

    [Fact]
    public async Task Negotiate_without_token_returns_401_problem_unauthorized()
    {
        var response = await Client.PostAsync("/hubs/kitchen/negotiate?negotiateVersion=1", content: null);

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(401, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("unauthorized");
    }

    [Fact]
    public async Task Connecting_without_token_fails()
    {
        var act = () => Factory.ConnectKitchenHubAsync(token: null);

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Kitchen_user_connects()
    {
        var (_, _, kitchenToken) = await ArrangeAsync();

        (await ConnectAsync(kitchenToken)).State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task Owner_connects()
    {
        var (owner, _, _) = await ArrangeAsync();

        (await ConnectAsync(owner.Token)).State.Should().Be(HubConnectionState.Connected);
    }

    // ------------------------------------------------------------------ events

    [Fact]
    public async Task Checkout_sends_order_created_with_the_kitchen_order()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var connection = await ConnectAsync(kitchenToken);
        var created = HubTestHelpers.WaitForEventAsync<KitchenOrderDto>(connection, "OrderCreated");

        var placed = await PlaceMixedOrderAsync(owner, menu);

        var order = await created;
        order.PickupCode.Should().Be(placed.PickupCode);
        order.CustomerName.Should().Be("Maria");
        order.Status.Should().Be("paid");
        order.Parts.Should().BeEquivalentTo([
            new KitchenOrderPartDto("bar", "received", [new KitchenOrderItemDto("Caipirinha", 2)]),
            new KitchenOrderPartDto("kitchen", "received", [new KitchenOrderItemDto("Batata", 1)]),
        ]);
        order.Should().BeEquivalentTo((await Api.ListKitchenOrdersAsync(kitchenToken)).Single(), "the event carries the full snapshot");
    }

    [Fact]
    public async Task Part_change_sends_order_updated()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        await PlaceMixedOrderAsync(owner, menu);
        var order = (await Api.ListKitchenOrdersAsync(kitchenToken)).Single();
        var connection = await ConnectAsync(kitchenToken);
        var updated = HubTestHelpers.WaitForEventAsync<KitchenOrderDto>(connection, "OrderUpdated");

        (await Api.PutPartStatusAsync(kitchenToken, order.Id, "bar", "in_preparation")).StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await updated;
        payload.Id.Should().Be(order.Id);
        payload.Status.Should().Be("in_preparation");
        payload.Parts.Single(p => p.Station == "bar").Status.Should().Be("in_preparation");
    }

    [Fact]
    public async Task Pickup_sends_order_picked_up_with_id_and_pickup_code()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var placed = await PlaceMixedOrderAsync(owner, menu);
        var order = (await Api.ListKitchenOrdersAsync(kitchenToken)).Single();
        await Api.MakeReadyAsync(kitchenToken, order);
        var connection = await ConnectAsync(kitchenToken);
        var pickedUp = HubTestHelpers.WaitForEventAsync<OrderPickedUpEventDto>(connection, "OrderPickedUp");

        (await Api.PutPickupAsync(kitchenToken, order)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await pickedUp).Should().Be(new OrderPickedUpEventDto(order.Id, placed.PickupCode));
    }

    [Fact]
    public async Task Owner_connection_receives_the_events_of_its_establishment()
    {
        var (owner, menu, _) = await ArrangeAsync();
        var connection = await ConnectAsync(owner.Token);
        var created = HubTestHelpers.WaitForEventAsync<KitchenOrderDto>(connection, "OrderCreated");

        var placed = await PlaceMixedOrderAsync(owner, menu);

        (await created).PickupCode.Should().Be(placed.PickupCode);
    }

    // ------------------------------------------------------------------ isolation

    [Fact]
    public async Task Isolation_connection_of_b_receives_no_event_of_a_within_two_seconds()
    {
        var (ownerA, menuA, kitchenA) = await ArrangeAsync();
        var (_, _, kitchenB) = await ArrangeAsync();
        var connectionA = await ConnectAsync(kitchenA);
        var connectionB = await ConnectAsync(kitchenB);
        var receivedByB = HubTestHelpers.RecordEvents(connectionB, AllEvents);
        var pickedUpByA = HubTestHelpers.WaitForEventAsync<OrderPickedUpEventDto>(connectionA, "OrderPickedUp");

        // A goes through the whole flow: created, updated (×2) and picked up.
        await PlaceMixedOrderAsync(ownerA, menuA);
        var orderA = (await Api.ListKitchenOrdersAsync(kitchenA)).Single();
        await Api.MakeReadyAsync(kitchenA, orderA);
        (await Api.PutPickupAsync(kitchenA, orderA)).StatusCode.Should().Be(HttpStatusCode.OK);

        await pickedUpByA; // sanity: events are flowing to A
        await Task.Delay(TimeSpan.FromSeconds(2));

        receivedByB.Should().BeEmpty("events of establishment A must never reach establishment B");
    }
}
