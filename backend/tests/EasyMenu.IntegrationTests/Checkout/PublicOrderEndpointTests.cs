using System.Net;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Checkout;

/// <summary>GET /api/public/orders/{publicToken} (FR-029, FR-031a; SC-006 snapshot). Anonymous.</summary>
public sealed class PublicOrderEndpointTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private static string SlugOf(RegisteredOwner owner) => owner.Response.User.Establishment.Slug;

    private async Task<(RegisteredOwner Owner, DefaultMenu Menu, PublicOrderDto Order)> ArrangeAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var order = await Api.PlaceOrderAsync(SlugOf(owner), "Maria",
            new PlaceOrderLine(menu.Caipirinha.Id, 2), new PlaceOrderLine(menu.Batata.Id, 1));
        return (owner, menu, order);
    }

    private async Task<PublicOrderDto> GetAsync(string publicToken) =>
        await ApiDriver.ReadSuccessAsync<PublicOrderDto>(await Api.GetPublicOrderResponseAsync(publicToken), "public order");

    [Fact]
    public async Task Returns_lines_total_status_and_establishment()
    {
        var (owner, _, placed) = await ArrangeAsync();

        var response = await Api.GetPublicOrderResponseAsync(placed.PublicToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var order = await ApiDriver.ReadSuccessAsync<PublicOrderDto>(response, "public order");
        order.PublicToken.Should().Be(placed.PublicToken);
        order.PickupCode.Should().Be(placed.PickupCode);
        order.CustomerName.Should().Be("Maria");
        order.Status.Should().Be("paid");
        order.TotalCents.Should().Be(6980);
        order.CreatedAt.Should().BeCloseTo(placed.CreatedAt, TimeSpan.FromMilliseconds(1));
        order.Establishment.Should().Be(new PublicEstablishmentDto(owner.Response.User.Establishment.Name, SlugOf(owner)));
        order.Items.Should().BeEquivalentTo([
            new OrderLineDto("Caipirinha", 1990, 2, 3980),
            new OrderLineDto("Batata", 3000, 1, 3000),
        ]);
    }

    [Fact]
    public async Task Snapshot_order_does_not_change_after_rename_price_change_and_deletion()
    {
        var (owner, menu, placed) = await ArrangeAsync();
        var admin = Api.CreateClient(owner.Token);
        var before = await GetAsync(placed.PublicToken);

        (await admin.PutAsJsonAsync($"/api/menu-items/{menu.Caipirinha.Id}",
            new MenuItemRequest(menu.Caipirinha.CategoryId, "Caipirinha Premium", null, 9900, null, true, "bar"), ApiDriver.Json))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/menu-items/{menu.Batata.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await GetAsync(placed.PublicToken);

        after.Should().BeEquivalentTo(before, "orders keep the name and price of the purchase (constitution IV, SC-006)");
        after.Items.Select(i => i.Name).Should().BeEquivalentTo(["Caipirinha", "Batata"]);
        after.TotalCents.Should().Be(6980);
    }

    [Fact]
    public async Task Invented_token_returns_404_order_not_found()
    {
        var problem = await ApiDriver.ReadProblemAsync(await Api.GetPublicOrderResponseAsync(new string('A', 43)));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("order_not_found");
    }

    [Fact]
    public async Task Pickup_code_used_as_token_returns_404()
    {
        var (_, _, placed) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.GetPublicOrderResponseAsync(placed.PickupCode));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("order_not_found");
    }

    [Fact]
    public async Task Each_token_only_opens_its_own_order()
    {
        var (_, _, first) = await ArrangeAsync();
        var otherOwner = await Api.RegisterOwnerAsync();
        var otherMenu = await Api.CreateMenuAsync(otherOwner.Token);
        var second = await Api.PlaceOrderAsync(SlugOf(otherOwner), "João", new PlaceOrderLine(otherMenu.Batata.Id, 3));

        first.PublicToken.Should().NotBe(second.PublicToken);
        (await GetAsync(first.PublicToken)).CustomerName.Should().Be("Maria");
        (await GetAsync(second.PublicToken)).Should().BeEquivalentTo(second);
    }
}
