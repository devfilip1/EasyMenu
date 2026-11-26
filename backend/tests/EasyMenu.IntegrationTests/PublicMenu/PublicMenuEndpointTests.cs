using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.PublicMenu;

/// <summary>GET /api/public/establishments/{slug}/menu (FR-012, FR-013, FR-038). Anonymous.</summary>
public sealed class PublicMenuEndpointTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private static string SlugOf(RegisteredOwner owner) => owner.Response.User.Establishment.Slug;

    private async Task<(RegisteredOwner Owner, DefaultMenu Menu)> ArrangeAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        return (owner, await Api.CreateMenuAsync(owner.Token));
    }

    [Fact]
    public async Task Anonymous_request_returns_200_with_the_establishment()
    {
        var (owner, _) = await ArrangeAsync();

        var response = await Api.GetPublicMenuResponseAsync(SlugOf(owner));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var menu = await ApiDriver.ReadSuccessAsync<PublicMenuDto>(response, "public menu");
        menu.Establishment.Should().Be(new PublicEstablishmentDto(owner.Response.User.Establishment.Name, SlugOf(owner)));
    }

    [Fact]
    public async Task Categories_and_items_follow_the_owners_order()
    {
        var (owner, menu) = await ArrangeAsync();
        var client = Api.CreateClient(owner.Token);
        var mojito = await Api.CreateMenuItemAsync(owner.Token, menu.Drinks.Id, "Mojito", 3200, station: "bar");
        (await client.PutAsJsonAsync("/api/categories/order", new ReorderRequest([menu.Porcoes.Id, menu.Drinks.Id]), ApiDriver.Json))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync($"/api/categories/{menu.Drinks.Id}/items/order",
            new ReorderRequest([mojito.Id, menu.Caipirinha.Id, menu.Chopp.Id]), ApiDriver.Json))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var publicMenu = await Api.GetPublicMenuAsync(SlugOf(owner));

        publicMenu.Categories.Select(c => c.Name).Should().Equal("Porções", "Drinks");
        publicMenu.Categories[1].Items.Select(i => i.Name).Should().Equal("Mojito", "Caipirinha", "Chopp");
    }

    [Fact]
    public async Task Category_with_only_sold_out_items_is_hidden()
    {
        var (owner, menu) = await ArrangeAsync();
        (await Api.CreateClient(owner.Token).PutAsJsonAsync($"/api/menu-items/{menu.Batata.Id}/availability",
            new AvailabilityRequest(false), ApiDriver.Json)).StatusCode.Should().Be(HttpStatusCode.OK);

        var publicMenu = await Api.GetPublicMenuAsync(SlugOf(owner));

        publicMenu.Categories.Select(c => c.Id).Should().Equal(menu.Drinks.Id);
    }

    [Fact]
    public async Task Empty_category_is_hidden()
    {
        var (owner, menu) = await ArrangeAsync();
        await Api.CreateCategoryAsync(owner.Token, "Sobremesas");

        var publicMenu = await Api.GetPublicMenuAsync(SlugOf(owner));

        publicMenu.Categories.Select(c => c.Id).Should().Equal(menu.Drinks.Id, menu.Porcoes.Id);
    }

    [Fact]
    public async Task Sold_out_item_is_listed_as_unavailable_in_a_category_with_available_items()
    {
        var (owner, menu) = await ArrangeAsync();

        var drinks = (await Api.GetPublicMenuAsync(SlugOf(owner))).Categories.Single(c => c.Id == menu.Drinks.Id);

        drinks.Items.Select(i => i.Id).Should().Equal(menu.Caipirinha.Id, menu.Chopp.Id);
        drinks.Items.Single(i => i.Id == menu.Chopp.Id).IsAvailable.Should().BeFalse();
        drinks.Items.Single(i => i.Id == menu.Caipirinha.Id).IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Items_expose_promotion_and_effective_price()
    {
        var (owner, menu) = await ArrangeAsync();

        var items = (await Api.GetPublicMenuAsync(SlugOf(owner))).Categories.SelectMany(c => c.Items).ToList();

        items.Single(i => i.Id == menu.Caipirinha.Id).Should().Be(new PublicMenuItemDto(
            menu.Caipirinha.Id, "Caipirinha", null, null, 2500, 1990, 1990, true));
        items.Single(i => i.Id == menu.Batata.Id).Should().Be(new PublicMenuItemDto(
            menu.Batata.Id, "Batata", null, null, 3000, null, 3000, true));
    }

    [Fact]
    public async Task Json_does_not_expose_station_position_or_category_id()
    {
        var (owner, _) = await ArrangeAsync();

        var body = await (await Api.GetPublicMenuResponseAsync(SlugOf(owner))).Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var names = PropertyNames(doc.RootElement).ToHashSet(StringComparer.OrdinalIgnoreCase);
        names.Should().Contain("effectivePriceCents", "sanity check: the menu was read");
        names.Should().NotContain(["station", "position", "categoryId", "establishmentId"], "internal fields stay private (FR-038): {0}", body);
    }

    [Fact]
    public async Task Unknown_slug_returns_404_establishment_not_found()
    {
        var problem = await ApiDriver.ReadProblemAsync(await Api.GetPublicMenuResponseAsync("nao-existe"));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("establishment_not_found");
    }

    [Fact]
    public async Task Menu_without_categories_returns_an_empty_list()
    {
        var owner = await Api.RegisterOwnerAsync();

        var publicMenu = await Api.GetPublicMenuAsync(SlugOf(owner));

        publicMenu.Categories.Should().BeEmpty();
        publicMenu.Establishment.Slug.Should().Be(SlugOf(owner));
    }

    [Fact]
    public async Task Isolation_data_of_b_does_not_appear_in_the_menu_of_a()
    {
        var (ownerA, menuA) = await ArrangeAsync();
        var (ownerB, menuB) = await ArrangeAsync();
        await Api.CreateMenuItemAsync(ownerB.Token, menuB.Drinks.Id, "Exclusivo do B", 999, station: "bar");

        var publicA = await Api.GetPublicMenuAsync(SlugOf(ownerA));
        var publicB = await Api.GetPublicMenuAsync(SlugOf(ownerB));

        publicA.Categories.Select(c => c.Id).Should().Equal(menuA.Drinks.Id, menuA.Porcoes.Id);
        publicA.Categories.SelectMany(c => c.Items).Select(i => i.Id)
            .Should().BeEquivalentTo([menuA.Caipirinha.Id, menuA.Chopp.Id, menuA.Batata.Id]);
        publicB.Categories.SelectMany(c => c.Items).Select(i => i.Name).Should().Contain("Exclusivo do B");
    }

    [Fact]
    public async Task Owner_token_of_another_establishment_does_not_change_the_result()
    {
        var (ownerA, menuA) = await ArrangeAsync();
        var ownerB = await Api.RegisterOwnerAsync();

        var response = await Api.GetPublicMenuResponseAsync(SlugOf(ownerA), Api.CreateClient(ownerB.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiDriver.ReadSuccessAsync<PublicMenuDto>(response, "menu")).Categories.Select(c => c.Id)
            .Should().Equal(menuA.Drinks.Id, menuA.Porcoes.Id);
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => PropertyNames(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
        _ => [],
    };
}
