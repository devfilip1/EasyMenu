using System.Net;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Orders;

/// <summary>GET /api/orders and /api/orders/{orderId} (FR-035, US5). Owner only; tenant isolated.</summary>
public sealed class OrdersEndpointsTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<(RegisteredOwner Owner, DefaultMenu Menu, string KitchenToken)> ArrangeAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        return (owner, menu, await Api.CreateKitchenTokenAsync(owner.EstablishmentId));
    }

    /// <summary>Places <paramref name="count"/> orders one minute apart; returns them oldest first.</summary>
    private async Task<List<PublicOrderDto>> PlaceSpacedOrdersAsync(RegisteredOwner owner, DefaultMenu menu, int count)
    {
        var orders = new List<PublicOrderDto>();
        for (var i = 0; i < count; i++)
        {
            orders.Add(await Api.PlaceOrderAsync(owner.Response.User.Establishment.Slug, $"Cliente {i}",
                new PlaceOrderLine(menu.Batata.Id, i + 1)));
            Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        return orders;
    }

    private async Task PickUpAsync(string kitchenToken, string pickupCode)
    {
        var order = (await Api.ListKitchenOrdersAsync(kitchenToken)).Single(o => o.PickupCode == pickupCode);
        await Api.MakeReadyAsync(kitchenToken, order);
        (await Api.PutPickupAsync(kitchenToken, order)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------ list

    [Fact]
    public async Task List_is_ordered_from_newest_to_oldest_with_default_paging()
    {
        var (owner, menu, _) = await ArrangeAsync();
        var placed = await PlaceSpacedOrdersAsync(owner, menu, 3);

        var page = await Api.ListOrdersAsync(owner.Token);

        page.Items.Select(o => o.PickupCode).Should().Equal(placed[2].PickupCode, placed[1].PickupCode, placed[0].PickupCode);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(20);
        page.TotalCount.Should().Be(3);
        var newest = page.Items[0];
        newest.CustomerName.Should().Be("Cliente 2");
        newest.Status.Should().Be("paid");
        newest.TotalCents.Should().Be(9000);
        newest.CreatedAt.Should().BeCloseTo(placed[2].CreatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Status_filter_returns_only_matching_orders()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var placed = await PlaceSpacedOrdersAsync(owner, menu, 3);
        await PickUpAsync(kitchenToken, placed[0].PickupCode);
        await PickUpAsync(kitchenToken, placed[2].PickupCode);

        var pickedUp = await Api.ListOrdersAsync(owner.Token, "?status=picked_up");
        var paid = await Api.ListOrdersAsync(owner.Token, "?status=paid");

        pickedUp.Items.Select(o => o.PickupCode).Should().Equal(placed[2].PickupCode, placed[0].PickupCode);
        pickedUp.Items.Should().OnlyContain(o => o.Status == "picked_up");
        pickedUp.TotalCount.Should().Be(2);
        paid.Items.Select(o => o.PickupCode).Should().Equal(placed[1].PickupCode);
        paid.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Paging_returns_the_requested_slice_and_the_total_count()
    {
        var (owner, menu, _) = await ArrangeAsync();
        var placed = await PlaceSpacedOrdersAsync(owner, menu, 5);

        var second = await Api.ListOrdersAsync(owner.Token, "?page=2&pageSize=2");
        var last = await Api.ListOrdersAsync(owner.Token, "?page=3&pageSize=2");
        var beyond = await Api.ListOrdersAsync(owner.Token, "?page=4&pageSize=2");

        second.Should().BeEquivalentTo(new { Page = 2, PageSize = 2, TotalCount = 5 });
        second.Items.Select(o => o.PickupCode).Should().Equal(placed[2].PickupCode, placed[1].PickupCode);
        last.Items.Select(o => o.PickupCode).Should().Equal(placed[0].PickupCode);
        beyond.Items.Should().BeEmpty();
        beyond.TotalCount.Should().Be(5);
    }

    [Theory]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=0")]
    [InlineData("?page=0")]
    [InlineData("?status=entregue")]
    public async Task Invalid_query_returns_400(string query)
    {
        var (owner, _, _) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.GetOrdersResponseAsync(owner.Token, query));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("validation_failed");
    }

    [Fact]
    public async Task Page_size_of_one_hundred_is_accepted()
    {
        var (owner, menu, _) = await ArrangeAsync();
        await PlaceSpacedOrdersAsync(owner, menu, 1);

        (await Api.ListOrdersAsync(owner.Token, "?pageSize=100")).PageSize.Should().Be(100);
    }

    // ------------------------------------------------------------------ detail

    [Fact]
    public async Task Get_by_id_returns_lines_with_snapshot_station_and_parts()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (placed, kitchenOrder) = await Api.PlaceKitchenOrderAsync(owner, "Maria",
            new PlaceOrderLine(menu.Caipirinha.Id, 2), new PlaceOrderLine(menu.Batata.Id, 1));
        (await Api.PutPartStatusAsync(kitchenToken, kitchenOrder.Id, "bar", "ready")).StatusCode.Should().Be(HttpStatusCode.OK);
        // Admin changes after the purchase must not affect the order (snapshot).
        (await Api.CreateClient(owner.Token).DeleteAsync($"/api/menu-items/{menu.Batata.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await Api.CreateClient(owner.Token).GetAsync($"/api/orders/{kitchenOrder.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var detail = await ApiDriver.ReadSuccessAsync<OrderDetailDto>(response, "order detail");
        detail.Id.Should().Be(kitchenOrder.Id);
        detail.PickupCode.Should().Be(placed.PickupCode);
        detail.CustomerName.Should().Be("Maria");
        detail.Status.Should().Be("in_preparation");
        detail.TotalCents.Should().Be(6980);
        detail.PickedUpAt.Should().BeNull();
        detail.Items.Should().BeEquivalentTo([
            new OrderDetailLineDto("Caipirinha", 1990, 2, 3980, "bar"),
            new OrderDetailLineDto("Batata", 3000, 1, 3000, "kitchen"),
        ]);
        detail.Parts.Should().BeEquivalentTo([new OrderPartStatusDto("bar", "ready"), new OrderPartStatusDto("kitchen", "received")]);
    }

    [Fact]
    public async Task Picked_up_order_detail_has_picked_up_at()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var placed = (await PlaceSpacedOrdersAsync(owner, menu, 1)).Single();
        await PickUpAsync(kitchenToken, placed.PickupCode);
        var id = (await Api.ListOrdersAsync(owner.Token)).Items.Single().Id;

        var detail = await ApiDriver.ReadSuccessAsync<OrderDetailDto>(await Api.CreateClient(owner.Token).GetAsync($"/api/orders/{id}"), "detail");

        detail.Status.Should().Be("picked_up");
        detail.PickedUpAt.Should().BeCloseTo(Factory.Clock.GetUtcNow(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Unknown_order_returns_404()
    {
        var (owner, _, _) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token).GetAsync($"/api/orders/{Guid.NewGuid()}"));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
    }

    // ------------------------------------------------------------------ roles

    [Fact]
    public async Task Kitchen_gets_403()
    {
        var (owner, menu, kitchenToken) = await ArrangeAsync();
        var (_, order) = await Api.PlaceKitchenOrderAsync(owner, null, new PlaceOrderLine(menu.Batata.Id, 1));

        (await Api.GetOrdersResponseAsync(kitchenToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Api.CreateClient(kitchenToken).GetAsync($"/api/orders/{order.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        (await Api.GetOrdersResponseAsync(null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync($"/api/orders/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------ isolation

    [Fact]
    public async Task Isolation_list_of_b_does_not_include_orders_of_a()
    {
        var (ownerA, menuA, _) = await ArrangeAsync();
        var (ownerB, menuB, _) = await ArrangeAsync();
        await PlaceSpacedOrdersAsync(ownerA, menuA, 2);
        var placedB = await PlaceSpacedOrdersAsync(ownerB, menuB, 1);

        var listB = await Api.ListOrdersAsync(ownerB.Token);

        listB.TotalCount.Should().Be(1);
        listB.Items.Should().ContainSingle().Which.PickupCode.Should().Be(placedB[0].PickupCode);
    }

    [Fact]
    public async Task Isolation_b_getting_an_order_of_a_returns_404()
    {
        var (ownerA, menuA, _) = await ArrangeAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        var (_, orderA) = await Api.PlaceKitchenOrderAsync(ownerA, null, new PlaceOrderLine(menuA.Batata.Id, 1));

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(ownerB.Token).GetAsync($"/api/orders/{orderA.Id}"));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
    }
}
