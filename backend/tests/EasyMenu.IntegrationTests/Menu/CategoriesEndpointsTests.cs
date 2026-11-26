using System.Net;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Menu;

/// <summary>/api/categories (FR-006, FR-011). Owner only; tenant isolated.</summary>
public sealed class CategoriesEndpointsTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Route = "/api/categories";

    [Fact]
    public async Task Create_returns_201_and_appends_to_the_end()
    {
        var owner = await Api.RegisterOwnerAsync();
        var client = Api.CreateClient(owner.Token);
        await Api.CreateCategoryAsync(owner.Token, "Drinks");
        await Api.CreateCategoryAsync(owner.Token, "Porções");

        var response = await client.PostAsJsonAsync(Route, new CategoryRequest("  Sobremesas "), ApiDriver.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await ApiDriver.ReadSuccessAsync<CategoryDto>(response, "create");
        created.Id.Should().NotBe(Guid.Empty);
        created.Name.Should().Be("Sobremesas");
        created.Position.Should().Be(2, "a new category goes to the end (position = n)");
        created.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task First_category_gets_position_zero()
    {
        var owner = await Api.RegisterOwnerAsync();

        (await Api.CreateCategoryAsync(owner.Token, "Drinks")).Position.Should().Be(0);
    }

    [Fact]
    public async Task List_respects_the_order()
    {
        var owner = await Api.RegisterOwnerAsync();
        var drinks = await Api.CreateCategoryAsync(owner.Token, "Drinks");
        var porcoes = await Api.CreateCategoryAsync(owner.Token, "Porções");
        var sobremesas = await Api.CreateCategoryAsync(owner.Token, "Sobremesas");

        var list = await Api.ListCategoriesAsync(owner.Token);

        list.Select(c => c.Id).Should().Equal(drinks.Id, porcoes.Id, sobremesas.Id);
        list.Select(c => c.Position).Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task List_reports_the_item_count()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);

        var list = await Api.ListCategoriesAsync(owner.Token);

        list.Single(c => c.Id == menu.Drinks.Id).ItemCount.Should().Be(2);
        list.Single(c => c.Id == menu.Porcoes.Id).ItemCount.Should().Be(1);
    }

    [Fact]
    public async Task Rename_returns_200_with_the_new_name()
    {
        var owner = await Api.RegisterOwnerAsync();
        var drinks = await Api.CreateCategoryAsync(owner.Token, "Drinks");

        var response = await Api.CreateClient(owner.Token)
            .PutAsJsonAsync($"{Route}/{drinks.Id}", new CategoryRequest(" Bebidas "), ApiDriver.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var renamed = await ApiDriver.ReadSuccessAsync<CategoryDto>(response, "rename");
        renamed.Should().Be(drinks with { Name = "Bebidas" });
        (await Api.ListCategoriesAsync(owner.Token)).Single().Name.Should().Be("Bebidas");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Invalid_name_returns_400_on_create_and_rename(string name)
    {
        var owner = await Api.RegisterOwnerAsync();
        var client = Api.CreateClient(owner.Token);
        var drinks = await Api.CreateCategoryAsync(owner.Token, "Drinks");

        var create = await ApiDriver.ReadProblemAsync(await client.PostAsJsonAsync(Route, new CategoryRequest(name), ApiDriver.Json));
        var rename = await ApiDriver.ReadProblemAsync(
            await client.PutAsJsonAsync($"{Route}/{drinks.Id}", new CategoryRequest(name), ApiDriver.Json));

        create.Status.Should().Be(400, create.RawBody);
        create.Code.Should().Be("validation_failed");
        rename.Status.Should().Be(400, rename.RawBody);
        rename.Code.Should().Be("validation_failed");
    }

    [Fact]
    public async Task Name_longer_than_sixty_characters_returns_400()
    {
        var owner = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token)
            .PostAsJsonAsync(Route, new CategoryRequest(new string('x', 61)), ApiDriver.Json));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("validation_failed");
    }

    [Fact]
    public async Task Rename_of_unknown_category_returns_404()
    {
        var owner = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token)
            .PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", new CategoryRequest("X"), ApiDriver.Json));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
    }

    // ------------------------------------------------------------------ reorder

    [Fact]
    public async Task Reorder_with_the_complete_list_returns_204_and_changes_the_order()
    {
        var owner = await Api.RegisterOwnerAsync();
        var a = await Api.CreateCategoryAsync(owner.Token, "A");
        var b = await Api.CreateCategoryAsync(owner.Token, "B");
        var c = await Api.CreateCategoryAsync(owner.Token, "C");

        var response = await Api.CreateClient(owner.Token)
            .PutAsJsonAsync($"{Route}/order", new ReorderRequest([c.Id, a.Id, b.Id]), ApiDriver.Json);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var list = await Api.ListCategoriesAsync(owner.Token);
        list.Select(x => x.Id).Should().Equal(c.Id, a.Id, b.Id);
        list.Select(x => x.Position).Should().Equal(0, 1, 2);
    }

    public static TheoryData<string> InvalidOrderLists => ["incomplete", "duplicate", "other_tenant", "unknown", "empty"];

    [Theory]
    [MemberData(nameof(InvalidOrderLists))]
    public async Task Reorder_with_an_invalid_list_returns_400_invalid_order_list(string kind)
    {
        var owner = await Api.RegisterOwnerAsync();
        var other = await Api.RegisterOwnerAsync();
        var a = await Api.CreateCategoryAsync(owner.Token, "A");
        var b = await Api.CreateCategoryAsync(owner.Token, "B");
        var c = await Api.CreateCategoryAsync(owner.Token, "C");
        var foreign = await Api.CreateCategoryAsync(other.Token, "De outro bar");

        Guid[] ids = kind switch
        {
            "incomplete" => [c.Id, a.Id],
            "duplicate" => [c.Id, a.Id, b.Id, a.Id],
            "other_tenant" => [c.Id, a.Id, b.Id, foreign.Id],
            "unknown" => [c.Id, a.Id, b.Id, Guid.NewGuid()],
            "empty" => [],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token)
            .PutAsJsonAsync($"{Route}/order", new ReorderRequest(ids), ApiDriver.Json));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("invalid_order_list");
        (await Api.ListCategoriesAsync(owner.Token)).Select(x => x.Id).Should().Equal([a.Id, b.Id, c.Id], "a rejected reorder changes nothing");
        (await Api.ListCategoriesAsync(other.Token)).Single().Position.Should().Be(0, "the other tenant is untouched");
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Delete_of_an_empty_category_returns_204()
    {
        var owner = await Api.RegisterOwnerAsync();
        var drinks = await Api.CreateCategoryAsync(owner.Token, "Drinks");
        var porcoes = await Api.CreateCategoryAsync(owner.Token, "Porções");

        var response = await Api.CreateClient(owner.Token).DeleteAsync($"{Route}/{drinks.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await Api.ListCategoriesAsync(owner.Token)).Select(c => c.Id).Should().Equal(porcoes.Id);
    }

    [Fact]
    public async Task Delete_of_unknown_category_returns_404()
    {
        var owner = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token).DeleteAsync($"{Route}/{Guid.NewGuid()}"));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
    }

    // ------------------------------------------------------------------ roles

    public static TheoryData<string, string> Operations => new()
    {
        { "GET", Route },
        { "POST", Route },
        { "PUT", $"{Route}/order" },
        { "PUT", $"{Route}/{{id}}" },
        { "DELETE", $"{Route}/{{id}}" },
    };

    private static HttpRequestMessage Request(string method, string route, Guid id)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), route.Replace("{id}", id.ToString()));
        object? body = method switch
        {
            "POST" => new CategoryRequest("Nova"),
            "PUT" when route.EndsWith("/order") => new ReorderRequest([id]),
            "PUT" => new CategoryRequest("Renomeada"),
            _ => null,
        };
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: ApiDriver.Json);
        }

        return request;
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Kitchen_gets_403(string method, string route)
    {
        var owner = await Api.RegisterOwnerAsync();
        var drinks = await Api.CreateCategoryAsync(owner.Token, "Drinks");
        var kitchen = Api.CreateClient(await Api.CreateKitchenTokenAsync(owner.EstablishmentId));

        var response = await kitchen.SendAsync(Request(method, route, drinks.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Api.ListCategoriesAsync(owner.Token)).Should().ContainSingle().Which.Should().Be(drinks);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Anonymous_gets_401(string method, string route)
    {
        var owner = await Api.RegisterOwnerAsync();
        var drinks = await Api.CreateCategoryAsync(owner.Token, "Drinks");

        var response = await Client.SendAsync(Request(method, route, drinks.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------ isolation

    [Fact]
    public async Task Isolation_b_renaming_a_category_of_a_returns_404()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        var drinksA = await Api.CreateCategoryAsync(ownerA.Token, "Drinks");

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(ownerB.Token)
            .PutAsJsonAsync($"{Route}/{drinksA.Id}", new CategoryRequest("Hackeada"), ApiDriver.Json));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
        (await Api.ListCategoriesAsync(ownerA.Token)).Single().Name.Should().Be("Drinks");
    }

    [Fact]
    public async Task Isolation_b_deleting_a_category_of_a_returns_404()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        var drinksA = await Api.CreateCategoryAsync(ownerA.Token, "Drinks");

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(ownerB.Token).DeleteAsync($"{Route}/{drinksA.Id}"));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
        (await Api.ListCategoriesAsync(ownerA.Token)).Should().ContainSingle().Which.Id.Should().Be(drinksA.Id);
    }

    [Fact]
    public async Task Isolation_list_of_b_does_not_include_categories_of_a()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        await Api.CreateCategoryAsync(ownerA.Token, "Drinks A");
        await Api.CreateCategoryAsync(ownerA.Token, "Porções A");
        var drinksB = await Api.CreateCategoryAsync(ownerB.Token, "Drinks B");

        var listB = await Api.ListCategoriesAsync(ownerB.Token);

        listB.Should().ContainSingle().Which.Should().Be(drinksB);
        drinksB.Position.Should().Be(0, "positions are per tenant");
    }
}
