using System.Net;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Kitchen;

/// <summary>/api/kitchen/... (FR-022..FR-028, FR-033a). Kitchen or Owner; tenant isolated.</summary>
public sealed class KitchenEndpointsTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Route = "/api/kitchen/orders";

    private async Task<(RegisteredOwner Owner, DefaultMenu Menu, string KitchenToken)> ArrangeAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        return (owner, menu, await Api.CreateKitchenTokenAsync(owner.EstablishmentId));
    }

    private Task<(PublicOrderDto Public, KitchenOrderDto Kitchen)> PlaceMixedOrderAsync(RegisteredOwner owner, DefaultMenu menu) =>
        Api.PlaceKitchenOrderAsync(owner, "Maria",
            new PlaceOrderLine(menu.Caipirinha.Id, 2), new PlaceOrderLine(menu.Batata.Id, 1));

    private static async Task<KitchenOrderDto> ReadOrderAsync(HttpResponseMessage response, string what)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ApiDriver.ReadSuccessAsync<KitchenOrderDto>(response, what);
    }

    // ------------------------------------------------------------------ list

    [Fact]
    public async Task List_shows_active_orders_oldest_first_with_each_part_only_with_its_station_items()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var first = await Api.PlaceOrderAsync(owner.Response.User.Establishment.Slug, "Primeiro",
            new PlaceOrderLine(menu.Caipirinha.Id, 2), new PlaceOrderLine(menu.Batata.Id, 1));
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await Api.PlaceOrderAsync(owner.Response.User.Establishment.Slug, null, new PlaceOrderLine(menu.Batata.Id, 3));
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var third = await Api.PlaceOrderAsync(owner.Response.User.Establishment.Slug, null, new PlaceOrderLine(menu.Caipirinha.Id, 1));

        var list = await Api.ListKitchenOrdersAsync(kitchenToken);

        list.Select(o => o.PickupCode).Should().Equal(first.PickupCode, second.PickupCode, third.PickupCode);
        var mixed = list[0];
        mixed.CustomerName.Should().Be("Primeiro");
        mixed.Status.Should().Be("paid");
        mixed.CreatedAt.Should().BeCloseTo(first.CreatedAt, TimeSpan.FromMilliseconds(1));
        mixed.Parts.Should().BeEquivalentTo([
            new KitchenOrderPartDto("bar", "received", [new KitchenOrderItemDto("Caipirinha", 2)]),
            new KitchenOrderPartDto("kitchen", "received", [new KitchenOrderItemDto("Batata", 1)]),
        ]);
        list[1].Parts.Should().ContainSingle().Which.Station.Should().Be("kitchen");
        list[2].Parts.Should().ContainSingle().Which.Station.Should().Be("bar");
    }

    [Fact]
    public async Task Owner_can_use_the_kitchen_panel()
    {
        var (owner, menu, _) = await ArrangeAsync();
        var (placed, order) = await PlaceMixedOrderAsync(owner, menu);

        (await Api.ListKitchenOrdersAsync(owner.Token)).Should().ContainSingle().Which.PickupCode.Should().Be(placed.PickupCode);
        (await Api.PutPartStatusAsync(owner.Token, order.Id, "bar", "in_preparation")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Anonymous_gets_401_on_every_operation()
    {
        var (owner, menu, _) = await ArrangeAsync();
        var (_, order) = await PlaceMixedOrderAsync(owner, menu);

        (await Client.GetAsync(Route)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Api.PutPartStatusAsync(null, order.Id, "bar", "ready")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Api.PutPickupAsync(null, order.Id)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Api.ListKitchenOrdersAsync(owner.Token)).Single().Status.Should().Be("paid");
    }

    // ------------------------------------------------------------------ part status

    [Fact]
    public async Task Moving_the_bar_part_to_in_preparation_returns_the_kitchen_order()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (placed, order) = await PlaceMixedOrderAsync(owner, menu);

        var updated = await ReadOrderAsync(await Api.PutPartStatusAsync(kitchenToken, order.Id, "bar", "in_preparation"), "bar");

        updated.Id.Should().Be(order.Id);
        updated.Status.Should().Be("in_preparation");
        updated.Parts.Single(p => p.Station == "bar").Status.Should().Be("in_preparation");
        updated.Parts.Single(p => p.Station == "kitchen").Status.Should().Be("received");
        (await ApiDriver.ReadSuccessAsync<PublicOrderDto>(await Api.GetPublicOrderResponseAsync(placed.PublicToken), "public"))
            .Status.Should().Be("in_preparation", "the consumer page sees the derived status");
    }

    [Fact]
    public async Task Repeating_the_current_status_returns_200()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (_, order) = await PlaceMixedOrderAsync(owner, menu);
        var first = await ReadOrderAsync(await Api.PutPartStatusAsync(kitchenToken, order.Id, "bar", "in_preparation"), "first");

        var again = await ReadOrderAsync(await Api.PutPartStatusAsync(kitchenToken, order.Id, "bar", "in_preparation"), "again");

        again.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task Ready_back_to_in_preparation_returns_409_invalid_status_transition()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (_, order) = await PlaceMixedOrderAsync(owner, menu);
        await ReadOrderAsync(await Api.PutPartStatusAsync(kitchenToken, order.Id, "bar", "ready"), "ready");

        var problem = await ApiDriver.ReadProblemAsync(await Api.PutPartStatusAsync(kitchenToken, order.Id, "bar", "in_preparation"));

        problem.Status.Should().Be(409, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("invalid_status_transition");
        (await Api.ListKitchenOrdersAsync(kitchenToken)).Single().Parts.Single(p => p.Station == "bar").Status.Should().Be("ready");
    }

    [Fact]
    public async Task Received_is_not_a_valid_target_status()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (_, order) = await PlaceMixedOrderAsync(owner, menu);

        var problem = await ApiDriver.ReadProblemAsync(await Api.PutPartStatusAsync(kitchenToken, order.Id, "bar", "received"));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("validation_failed");
    }

    [Fact]
    public async Task Part_of_a_station_absent_from_the_order_returns_404()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (_, order) = await Api.PlaceKitchenOrderAsync(owner, null, new PlaceOrderLine(menu.Caipirinha.Id, 1));

        var problem = await ApiDriver.ReadProblemAsync(await Api.PutPartStatusAsync(kitchenToken, order.Id, "kitchen", "ready"));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Unknown_order_returns_404()
    {
        var (_, _, kitchenToken) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.PutPartStatusAsync(kitchenToken, Guid.NewGuid(), "bar", "ready"));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
    }

    // ------------------------------------------------------------------ pickup

    [Fact]
    public async Task Pickup_before_ready_returns_409_order_not_ready()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (_, order) = await PlaceMixedOrderAsync(owner, menu);
        await ReadOrderAsync(await Api.PutPartStatusAsync(kitchenToken, order.Id, "bar", "ready"), "bar ready");

        var problem = await ApiDriver.ReadProblemAsync(await Api.PutPickupAsync(kitchenToken, order.Id));

        problem.Status.Should().Be(409, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("order_not_ready");
        (await Api.ListKitchenOrdersAsync(kitchenToken)).Should().ContainSingle();
    }

    [Fact]
    public async Task Pickup_after_ready_returns_200_and_removes_the_order_from_the_list()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (placed, order) = await PlaceMixedOrderAsync(owner, menu);
        var ready = await Api.MakeReadyAsync(kitchenToken, order);
        ready.Status.Should().Be("ready");

        var pickedUp = await ReadOrderAsync(await Api.PutPickupAsync(kitchenToken, order.Id), "pickup");

        pickedUp.Status.Should().Be("picked_up");
        (await Api.ListKitchenOrdersAsync(kitchenToken)).Should().BeEmpty();
        (await ApiDriver.ReadSuccessAsync<PublicOrderDto>(await Api.GetPublicOrderResponseAsync(placed.PublicToken), "public"))
            .Status.Should().Be("picked_up");
    }

    [Fact]
    public async Task Pickup_is_idempotent()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (_, order) = await PlaceMixedOrderAsync(owner, menu);
        await Api.MakeReadyAsync(kitchenToken, order);
        await ReadOrderAsync(await Api.PutPickupAsync(kitchenToken, order.Id), "first pickup");

        (await ReadOrderAsync(await Api.PutPickupAsync(kitchenToken, order.Id), "second pickup")).Status.Should().Be("picked_up");
    }

    [Fact]
    public async Task Pickup_code_is_reused_after_pickup()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        await using var host = Factory.WithPickupCodes("ABCD", "ABCD");
        using var client = host.CreateClient();
        var slug = owner.Response.User.Establishment.Slug;
        var request = new PlaceOrderRequest(null, [new PlaceOrderLine(menu.Batata.Id, 1)]);

        var first = await ApiDriver.ReadSuccessAsync<PublicOrderDto>(await Api.PostOrderAsync(slug, request, client), "first");
        first.PickupCode.Should().Be("ABCD");
        var order = (await Api.ListKitchenOrdersAsync(kitchenToken)).Single();
        await Api.MakeReadyAsync(kitchenToken, order);
        await ReadOrderAsync(await Api.PutPickupAsync(kitchenToken, order.Id), "pickup");

        var response = await Api.PostOrderAsync(slug, request, client);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        (await ApiDriver.ReadSuccessAsync<PublicOrderDto>(response, "second")).PickupCode
            .Should().Be("ABCD", "a picked-up order frees its code (FR-021)");
    }

    // ------------------------------------------------------------------ concurrency

    [Fact]
    public async Task Concurrent_ready_requests_on_the_same_part_both_succeed()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (_, order) = await PlaceMixedOrderAsync(owner, menu);

        var responses = await Task.WhenAll(
            Api.PutPartStatusAsync(kitchenToken, order.Id, "kitchen", "ready"),
            Api.PutPartStatusAsync(kitchenToken, order.Id, "kitchen", "ready"));

        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        responses.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK, string.Join(" | ", bodies));
        (await Api.ListKitchenOrdersAsync(kitchenToken)).Single().Parts.Single(p => p.Station == "kitchen").Status.Should().Be("ready");
    }

    [Fact]
    public async Task Concurrent_ready_on_different_parts_ends_ready()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (_, order) = await PlaceMixedOrderAsync(owner, menu);

        var responses = await Task.WhenAll(
            Api.PutPartStatusAsync(kitchenToken, order.Id, "bar", "ready"),
            Api.PutPartStatusAsync(kitchenToken, order.Id, "kitchen", "ready"));

        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        responses.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK, string.Join(" | ", bodies));
        (await Api.ListKitchenOrdersAsync(kitchenToken)).Single().Status.Should().Be("ready", "the derived status must see both parts");
    }

    // ------------------------------------------------------------------ isolation

    [Fact]
    public async Task Isolation_kitchen_of_b_on_order_routes_of_a_returns_404()
    {
        var (ownerA, menuA, kitchenA) = await ArrangeAsync();
        var (_, _, kitchenB) = await ArrangeAsync();
        var (_, orderA) = await PlaceMixedOrderAsync(ownerA, menuA);

        var part = await ApiDriver.ReadProblemAsync(await Api.PutPartStatusAsync(kitchenB, orderA.Id, "bar", "ready"));
        var pickup = await ApiDriver.ReadProblemAsync(await Api.PutPickupAsync(kitchenB, orderA.Id));

        part.Status.Should().Be(404, part.RawBody);
        part.Code.Should().Be("not_found");
        pickup.Status.Should().Be(404, pickup.RawBody);
        pickup.Code.Should().Be("not_found");
        (await Api.ListKitchenOrdersAsync(kitchenA)).Single().Should().BeEquivalentTo(orderA, "A's order is untouched");
    }

    [Fact]
    public async Task Isolation_list_of_b_does_not_include_orders_of_a()
    {
        var (ownerA, menuA, _) = await ArrangeAsync();
        var (ownerB, menuB, kitchenB) = await ArrangeAsync();
        await PlaceMixedOrderAsync(ownerA, menuA);
        var (placedB, _) = await PlaceMixedOrderAsync(ownerB, menuB);

        var listB = await Api.ListKitchenOrdersAsync(kitchenB);

        listB.Should().ContainSingle().Which.PickupCode.Should().Be(placedB.PickupCode);
    }

    [Fact]
    public async Task Isolation_owner_of_b_cannot_touch_orders_of_a()
    {
        var (ownerA, menuA, _) = await ArrangeAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        var (_, orderA) = await PlaceMixedOrderAsync(ownerA, menuA);

        (await Api.PutPartStatusAsync(ownerB.Token, orderA.Id, "bar", "ready")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Api.ListKitchenOrdersAsync(ownerB.Token)).Should().BeEmpty();
    }
}
