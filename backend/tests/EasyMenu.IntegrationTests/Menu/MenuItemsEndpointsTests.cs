using System.Net;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Menu;

/// <summary>/api/menu-items and item ordering per category (FR-007..FR-011). Owner only; tenant isolated.</summary>
public sealed class MenuItemsEndpointsTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Route = "/api/menu-items";

    private static MenuItemRequest RequestFor(MenuItemDto item) =>
        new(item.CategoryId, item.Name, item.Description, item.PriceCents, item.PromotionalPriceCents, item.IsAvailable, item.Station);

    // ------------------------------------------------------------------ create / get / list

    [Fact]
    public async Task Create_returns_201_with_effective_price_and_position_at_the_end_of_the_category()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);

        var response = await Api.CreateClient(owner.Token).PostAsJsonAsync(Route,
            new MenuItemRequest(menu.Drinks.Id, " Mojito ", "Hortelã", 3200, 2800, true, "bar"), ApiDriver.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var item = await ApiDriver.ReadSuccessAsync<MenuItemDto>(response, "create");
        item.Id.Should().NotBe(Guid.Empty);
        item.CategoryId.Should().Be(menu.Drinks.Id);
        item.Name.Should().Be("Mojito");
        item.Description.Should().Be("Hortelã");
        item.PhotoUrl.Should().BeNull();
        item.PriceCents.Should().Be(3200);
        item.PromotionalPriceCents.Should().Be(2800);
        item.EffectivePriceCents.Should().Be(2800);
        item.IsAvailable.Should().BeTrue();
        item.Station.Should().Be("bar");
        item.Position.Should().Be(2, "Drinks already has Caipirinha (0) and Chopp (1)");
    }

    [Fact]
    public async Task Default_menu_has_the_expected_effective_prices_and_positions()
    {
        var owner = await Api.RegisterOwnerAsync();

        var menu = await Api.CreateMenuAsync(owner.Token);

        menu.Caipirinha.EffectivePriceCents.Should().Be(1990);
        menu.Caipirinha.Position.Should().Be(0);
        menu.Chopp.EffectivePriceCents.Should().Be(1200);
        menu.Chopp.IsAvailable.Should().BeFalse();
        menu.Chopp.Position.Should().Be(1);
        menu.Batata.Station.Should().Be("kitchen");
        menu.Batata.Position.Should().Be(0, "positions are per category");
    }

    [Fact]
    public async Task Get_by_id_returns_the_item()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);

        var response = await Api.CreateClient(owner.Token).GetAsync($"{Route}/{menu.Caipirinha.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiDriver.ReadSuccessAsync<MenuItemDto>(response, "get")).Should().Be(menu.Caipirinha);
    }

    [Fact]
    public async Task Get_of_unknown_item_returns_404()
    {
        var owner = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token).GetAsync($"{Route}/{Guid.NewGuid()}"));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task List_is_ordered_by_category_and_position_and_includes_sold_out_items()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        // Reorder categories so Porções comes first: the list must follow the category order.
        (await Api.CreateClient(owner.Token).PutAsJsonAsync("/api/categories/order",
            new ReorderRequest([menu.Porcoes.Id, menu.Drinks.Id]), ApiDriver.Json)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await Api.ListMenuItemsAsync(owner.Token);

        list.Select(i => i.Name).Should().Equal("Batata", "Caipirinha", "Chopp");
    }

    [Fact]
    public async Task List_can_be_filtered_by_category()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);

        var drinks = await Api.ListMenuItemsAsync(owner.Token, menu.Drinks.Id);

        drinks.Select(i => i.Id).Should().Equal(menu.Caipirinha.Id, menu.Chopp.Id);
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task Put_updates_every_field()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var client = Api.CreateClient(owner.Token);

        var response = await client.PutAsJsonAsync($"{Route}/{menu.Caipirinha.Id}",
            new MenuItemRequest(menu.Drinks.Id, "Caipirinha de morango", "Morango", 2800, null, false, "kitchen"), ApiDriver.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var updated = await ApiDriver.ReadSuccessAsync<MenuItemDto>(response, "update");
        updated.Should().Be(menu.Caipirinha with
        {
            Name = "Caipirinha de morango",
            Description = "Morango",
            PriceCents = 2800,
            PromotionalPriceCents = null,
            EffectivePriceCents = 2800,
            IsAvailable = false,
            Station = "kitchen",
        });
        (await ApiDriver.ReadSuccessAsync<MenuItemDto>(await client.GetAsync($"{Route}/{menu.Caipirinha.Id}"), "get"))
            .Should().Be(updated);
    }

    [Fact]
    public async Task Changing_the_category_moves_the_item_to_the_end_of_the_new_one()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);

        var response = await Api.CreateClient(owner.Token).PutAsJsonAsync($"{Route}/{menu.Caipirinha.Id}",
            RequestFor(menu.Caipirinha) with { CategoryId = menu.Porcoes.Id }, ApiDriver.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var moved = await ApiDriver.ReadSuccessAsync<MenuItemDto>(response, "move");
        moved.CategoryId.Should().Be(menu.Porcoes.Id);
        moved.Position.Should().Be(1, "Porções already had Batata at position 0");
        (await Api.ListMenuItemsAsync(owner.Token, menu.Porcoes.Id)).Select(i => i.Id).Should().Equal(menu.Batata.Id, menu.Caipirinha.Id);
        (await Api.ListMenuItemsAsync(owner.Token, menu.Drinks.Id)).Select(i => i.Id).Should().Equal(menu.Chopp.Id);
    }

    [Theory]
    [InlineData(2500L, 2500L)]
    [InlineData(2500L, 3000L)]
    public async Task Invalid_promotional_price_returns_400_invalid_promotional_price(long priceCents, long promotionalPriceCents)
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var client = Api.CreateClient(owner.Token);
        var request = RequestFor(menu.Batata) with { PriceCents = priceCents, PromotionalPriceCents = promotionalPriceCents };

        var create = await ApiDriver.ReadProblemAsync(await client.PostAsJsonAsync(Route, request, ApiDriver.Json));
        var update = await ApiDriver.ReadProblemAsync(await client.PutAsJsonAsync($"{Route}/{menu.Batata.Id}", request, ApiDriver.Json));

        create.Status.Should().Be(400, create.RawBody);
        create.MediaType.Should().Be("application/problem+json");
        create.Code.Should().Be("invalid_promotional_price");
        update.Status.Should().Be(400, update.RawBody);
        update.Code.Should().Be("invalid_promotional_price");
        (await ApiDriver.ReadSuccessAsync<MenuItemDto>(await client.GetAsync($"{Route}/{menu.Batata.Id}"), "get"))
            .Should().Be(menu.Batata, "a rejected update changes nothing");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(10_000_001L)]
    public async Task Invalid_price_returns_400(long priceCents)
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token)
            .PostAsJsonAsync(Route, RequestFor(menu.Batata) with { PriceCents = priceCents }, ApiDriver.Json));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().BeOneOf("validation_failed", "invalid_price");
    }

    [Fact]
    public async Task Price_sent_as_decimal_is_rejected()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var body = new
        {
            categoryId = menu.Drinks.Id,
            name = "Gin",
            priceCents = 25.5,
            isAvailable = true,
            station = "bar",
        };

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token).PostAsJsonAsync(Route, body, ApiDriver.Json));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("validation_failed");
    }

    // ------------------------------------------------------------------ availability

    [Fact]
    public async Task Put_availability_toggles_sold_out()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var client = Api.CreateClient(owner.Token);

        var off = await client.PutAsJsonAsync($"{Route}/{menu.Caipirinha.Id}/availability", new AvailabilityRequest(false), ApiDriver.Json);
        off.StatusCode.Should().Be(HttpStatusCode.OK, await off.Content.ReadAsStringAsync());
        (await ApiDriver.ReadSuccessAsync<MenuItemDto>(off, "off")).Should().Be(menu.Caipirinha with { IsAvailable = false });

        var on = await client.PutAsJsonAsync($"{Route}/{menu.Caipirinha.Id}/availability", new AvailabilityRequest(true), ApiDriver.Json);
        (await ApiDriver.ReadSuccessAsync<MenuItemDto>(on, "on")).IsAvailable.Should().BeTrue();
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Delete_returns_204_and_removes_the_item()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var client = Api.CreateClient(owner.Token);

        var response = await client.DeleteAsync($"{Route}/{menu.Chopp.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await client.GetAsync($"{Route}/{menu.Chopp.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Api.ListMenuItemsAsync(owner.Token)).Select(i => i.Id).Should().NotContain(menu.Chopp.Id);
    }

    [Fact]
    public async Task Deleting_a_category_with_items_returns_409_category_not_empty()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token).DeleteAsync($"/api/categories/{menu.Drinks.Id}"));

        problem.Status.Should().Be(409, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("category_not_empty");
        (await Api.ListCategoriesAsync(owner.Token)).Select(c => c.Id).Should().Contain(menu.Drinks.Id);
    }

    [Fact]
    public async Task Category_can_be_deleted_after_its_items_are_removed()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var client = Api.CreateClient(owner.Token);
        await client.DeleteAsync($"{Route}/{menu.Batata.Id}");

        (await client.DeleteAsync($"/api/categories/{menu.Porcoes.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ------------------------------------------------------------------ item order

    [Fact]
    public async Task Reorder_items_of_a_category_returns_204()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var mojito = await Api.CreateMenuItemAsync(owner.Token, menu.Drinks.Id, "Mojito", 3200, station: "bar");

        var response = await Api.CreateClient(owner.Token).PutAsJsonAsync($"/api/categories/{menu.Drinks.Id}/items/order",
            new ReorderRequest([mojito.Id, menu.Chopp.Id, menu.Caipirinha.Id]), ApiDriver.Json);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var drinks = await Api.ListMenuItemsAsync(owner.Token, menu.Drinks.Id);
        drinks.Select(i => i.Id).Should().Equal(mojito.Id, menu.Chopp.Id, menu.Caipirinha.Id);
        drinks.Select(i => i.Position).Should().Equal(0, 1, 2);
    }

    public static TheoryData<string> InvalidItemOrderLists => ["incomplete", "duplicate", "other_category", "other_tenant", "unknown"];

    [Theory]
    [MemberData(nameof(InvalidItemOrderLists))]
    public async Task Reorder_items_with_an_invalid_list_returns_400_invalid_order_list(string kind)
    {
        var owner = await Api.RegisterOwnerAsync();
        var other = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var foreign = await Api.CreateMenuAsync(other.Token);

        Guid[] ids = kind switch
        {
            "incomplete" => [menu.Chopp.Id],
            "duplicate" => [menu.Chopp.Id, menu.Caipirinha.Id, menu.Chopp.Id],
            "other_category" => [menu.Chopp.Id, menu.Caipirinha.Id, menu.Batata.Id],
            "other_tenant" => [menu.Chopp.Id, menu.Caipirinha.Id, foreign.Caipirinha.Id],
            "unknown" => [menu.Chopp.Id, menu.Caipirinha.Id, Guid.NewGuid()],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token)
            .PutAsJsonAsync($"/api/categories/{menu.Drinks.Id}/items/order", new ReorderRequest(ids), ApiDriver.Json));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("invalid_order_list");
        (await Api.ListMenuItemsAsync(owner.Token, menu.Drinks.Id)).Select(i => i.Id)
            .Should().Equal([menu.Caipirinha.Id, menu.Chopp.Id], "a rejected reorder changes nothing");
    }

    [Fact]
    public async Task Reorder_items_of_unknown_category_returns_404()
    {
        var owner = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token)
            .PutAsJsonAsync($"/api/categories/{Guid.NewGuid()}/items/order", new ReorderRequest([]), ApiDriver.Json));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().BeOneOf("not_found", "category_not_found");
    }

    // ------------------------------------------------------------------ category of another tenant

    [Fact]
    public async Task Create_with_a_category_of_another_tenant_returns_404_category_not_found()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        var menuA = await Api.CreateMenuAsync(ownerA.Token);

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(ownerB.Token)
            .PostAsJsonAsync(Route, RequestFor(menuA.Batata), ApiDriver.Json));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("category_not_found");
        (await Api.ListMenuItemsAsync(ownerA.Token, menuA.Porcoes.Id)).Should().ContainSingle("nothing was added to A");
        (await Api.ListMenuItemsAsync(ownerB.Token)).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_with_unknown_category_returns_404_category_not_found()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token)
            .PostAsJsonAsync(Route, RequestFor(menu.Batata) with { CategoryId = Guid.NewGuid() }, ApiDriver.Json));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("category_not_found");
    }

    [Fact]
    public async Task Isolation_moving_own_item_to_a_category_of_another_tenant_returns_404_category_not_found()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        var menuA = await Api.CreateMenuAsync(ownerA.Token);
        var menuB = await Api.CreateMenuAsync(ownerB.Token);

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(ownerB.Token).PutAsJsonAsync(
            $"{Route}/{menuB.Batata.Id}", RequestFor(menuB.Batata) with { CategoryId = menuA.Drinks.Id }, ApiDriver.Json));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("category_not_found");
        (await Api.ListMenuItemsAsync(ownerA.Token, menuA.Drinks.Id)).Should().HaveCount(2);
    }

    // ------------------------------------------------------------------ roles

    public static TheoryData<string, string> Operations => new()
    {
        { "GET", Route },
        { "POST", Route },
        { "GET", $"{Route}/{{item}}" },
        { "PUT", $"{Route}/{{item}}" },
        { "DELETE", $"{Route}/{{item}}" },
        { "PUT", $"{Route}/{{item}}/availability" },
        { "PUT", "/api/categories/{category}/items/order" },
    };

    private static HttpRequestMessage Request(string method, string route, DefaultMenu menu)
    {
        var url = route.Replace("{item}", menu.Caipirinha.Id.ToString()).Replace("{category}", menu.Drinks.Id.ToString());
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        object? body = (method, route) switch
        {
            ("POST", _) => RequestFor(menu.Caipirinha) with { Name = "Intruso" },
            ("PUT", _) when route.EndsWith("/availability") => new AvailabilityRequest(false),
            ("PUT", _) when route.EndsWith("/order") => new ReorderRequest([menu.Chopp.Id, menu.Caipirinha.Id]),
            ("PUT", _) => RequestFor(menu.Caipirinha) with { Name = "Hackeado", PriceCents = 1, PromotionalPriceCents = null },
            _ => null,
        };
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: ApiDriver.Json);
        }

        return request;
    }

    private async Task AssertMenuUnchangedAsync(string token, DefaultMenu menu)
    {
        (await Api.ListMenuItemsAsync(token)).Should().BeEquivalentTo(
            [menu.Caipirinha, menu.Chopp, menu.Batata], options => options.WithStrictOrdering());
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Kitchen_gets_403(string method, string route)
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var kitchen = Api.CreateClient(await Api.CreateKitchenTokenAsync(owner.EstablishmentId));

        var response = await kitchen.SendAsync(Request(method, route, menu));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertMenuUnchangedAsync(owner.Token, menu);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Anonymous_gets_401(string method, string route)
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);

        var response = await Client.SendAsync(Request(method, route, menu));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertMenuUnchangedAsync(owner.Token, menu);
    }

    // ------------------------------------------------------------------ isolation

    public static TheoryData<string, string> ByIdOperations => new()
    {
        { "GET", $"{Route}/{{item}}" },
        { "PUT", $"{Route}/{{item}}" },
        { "DELETE", $"{Route}/{{item}}" },
        { "PUT", $"{Route}/{{item}}/availability" },
        { "PUT", "/api/categories/{category}/items/order" },
    };

    [Theory]
    [MemberData(nameof(ByIdOperations))]
    public async Task Isolation_every_by_id_operation_with_token_of_b_on_item_of_a_returns_404(string method, string route)
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        var menuA = await Api.CreateMenuAsync(ownerA.Token);
        var menuB = await Api.CreateMenuAsync(ownerB.Token);
        var request = Request(method, route, menuA);
        if (method == "PUT" && route == $"{Route}/{{item}}")
        {
            // Body points to B's own category, so only the item id is foreign.
            request.Content = JsonContent.Create(RequestFor(menuA.Caipirinha) with { CategoryId = menuB.Drinks.Id, Name = "Hackeado" },
                options: ApiDriver.Json);
        }

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(ownerB.Token).SendAsync(request));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        await AssertMenuUnchangedAsync(ownerA.Token, menuA);
    }

    [Fact]
    public async Task Isolation_list_of_b_does_not_include_items_of_a()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        var menuA = await Api.CreateMenuAsync(ownerA.Token);
        var menuB = await Api.CreateMenuAsync(ownerB.Token);

        var listB = await Api.ListMenuItemsAsync(ownerB.Token);
        var filteredByForeignCategory = await Api.ListMenuItemsAsync(ownerB.Token, menuA.Drinks.Id);

        listB.Select(i => i.Id).Should().BeEquivalentTo([menuB.Caipirinha.Id, menuB.Chopp.Id, menuB.Batata.Id]);
        filteredByForeignCategory.Should().BeEmpty();
    }
}
