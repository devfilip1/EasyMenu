using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using EasyMenu.Application.Abstractions;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyMenu.IntegrationTests.Checkout;

/// <summary>
/// POST /api/public/establishments/{slug}/orders (FR-016..FR-021; constitution IV and V): simulated payment,
/// total computed by the server with the current prices, sold-out/removed/foreign items block the purchase.
/// </summary>
public sealed partial class PlaceOrderTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    [GeneratedRegex("^[2-9A-HJKMNP-Z]{4}$")]
    private static partial Regex PickupCodeFormat();

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex PublicTokenFormat();

    private static string SlugOf(RegisteredOwner owner) => owner.Response.User.Establishment.Slug;

    private async Task<(RegisteredOwner Owner, DefaultMenu Menu)> ArrangeAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        return (owner, await Api.CreateMenuAsync(owner.Token));
    }

    [Fact]
    public async Task Returns_201_with_a_paid_public_order_and_the_server_computed_total()
    {
        var (owner, menu) = await ArrangeAsync();

        var response = await Api.PostOrderAsync(SlugOf(owner), new PlaceOrderRequest("  Maria ",
            [new PlaceOrderLine(menu.Caipirinha.Id, 2), new PlaceOrderLine(menu.Batata.Id, 1)]));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var order = await ApiDriver.ReadSuccessAsync<PublicOrderDto>(response, "place order");
        order.Status.Should().Be("paid");
        order.TotalCents.Should().Be(6980, "2×1990 (promotion) + 1×3000");
        order.PickupCode.Should().MatchRegex(PickupCodeFormat());
        order.PublicToken.Should().MatchRegex(PublicTokenFormat());
        order.CustomerName.Should().Be("Maria");
        order.CreatedAt.Should().BeCloseTo(Factory.Clock.GetUtcNow(), TimeSpan.FromSeconds(5));
        order.Establishment.Should().Be(new PublicEstablishmentDto(owner.Response.User.Establishment.Name, SlugOf(owner)));
        order.Items.Should().BeEquivalentTo([
            new OrderLineDto("Caipirinha", 1990, 2, 3980),
            new OrderLineDto("Batata", 3000, 1, 3000),
        ]);
        (await Api.CountOrdersAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Empty_customer_name_becomes_null()
    {
        var (owner, menu) = await ArrangeAsync();

        var order = await Api.PlaceOrderAsync(SlugOf(owner), "   ", new PlaceOrderLine(menu.Batata.Id, 1));

        order.CustomerName.Should().BeNull();
    }

    [Fact]
    public async Task Repeated_lines_are_grouped()
    {
        var (owner, menu) = await ArrangeAsync();

        var order = await Api.PlaceOrderAsync(SlugOf(owner), null,
            new PlaceOrderLine(menu.Batata.Id, 1), new PlaceOrderLine(menu.Batata.Id, 2));

        order.Items.Should().ContainSingle().Which.Should().Be(new OrderLineDto("Batata", 3000, 3, 9000));
        order.TotalCents.Should().Be(9000);
    }

    [Fact]
    public async Task Extra_fields_with_prices_totals_or_payment_data_are_ignored()
    {
        var (owner, menu) = await ArrangeAsync();
        var body = new
        {
            customerName = "Esperto",
            totalCents = 1,
            payment = new { cardNumber = "4111111111111111", amountCents = 1, approved = true },
            items = new object[]
            {
                new { menuItemId = menu.Caipirinha.Id, quantity = 2, unitPriceCents = 1, subtotalCents = 2 },
                new { menuItemId = menu.Batata.Id, quantity = 1, unitPriceCents = 1, priceCents = 1 },
            },
        };

        var response = await Api.PostOrderAsync(SlugOf(owner), body);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var order = await ApiDriver.ReadSuccessAsync<PublicOrderDto>(response, "place order");
        order.TotalCents.Should().Be(6980);
        order.Items.Select(i => i.UnitPriceCents).Should().BeEquivalentTo([1990L, 3000L]);
    }

    [Fact]
    public async Task Price_changed_in_the_admin_before_the_purchase_is_used()
    {
        var (owner, menu) = await ArrangeAsync();
        (await Api.CreateClient(owner.Token).PutAsJsonAsync($"/api/menu-items/{menu.Batata.Id}",
            new MenuItemRequest(menu.Batata.CategoryId, "Batata", null, 3500, null, true, "kitchen"), ApiDriver.Json))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await Api.PlaceOrderAsync(SlugOf(owner), null, new PlaceOrderLine(menu.Batata.Id, 2));

        order.Items.Single().Should().Be(new OrderLineDto("Batata", 3500, 2, 7000));
        order.TotalCents.Should().Be(7000);
    }

    [Fact]
    public async Task Promotion_removed_in_the_admin_before_the_purchase_is_not_applied()
    {
        var (owner, menu) = await ArrangeAsync();
        (await Api.CreateClient(owner.Token).PutAsJsonAsync($"/api/menu-items/{menu.Caipirinha.Id}",
            new MenuItemRequest(menu.Caipirinha.CategoryId, "Caipirinha", null, 2500, null, true, "bar"), ApiDriver.Json))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await Api.PlaceOrderAsync(SlugOf(owner), null, new PlaceOrderLine(menu.Caipirinha.Id, 1));

        order.TotalCents.Should().Be(2500);
    }

    [Fact]
    public async Task Customer_name_longer_than_sixty_characters_returns_400()
    {
        var (owner, menu) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.PostOrderAsync(SlugOf(owner),
            new PlaceOrderRequest(new string('m', 61), [new PlaceOrderLine(menu.Batata.Id, 1)])));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("validation_failed");
        (await Api.CountOrdersAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Empty_items_return_400_empty_cart()
    {
        var (owner, _) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.PostOrderAsync(SlugOf(owner), new PlaceOrderRequest(null, [])));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("empty_cart");
        (await Api.CountOrdersAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task Quantity_outside_one_to_ninety_nine_returns_400(int quantity)
    {
        var (owner, menu) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.PostOrderAsync(SlugOf(owner),
            new PlaceOrderRequest(null, [new PlaceOrderLine(menu.Batata.Id, quantity)])));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().BeOneOf("invalid_quantity", "validation_failed");
        (await Api.CountOrdersAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Grouped_quantity_above_ninety_nine_returns_400_invalid_quantity()
    {
        var (owner, menu) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.PostOrderAsync(SlugOf(owner),
            new PlaceOrderRequest(null, [new PlaceOrderLine(menu.Batata.Id, 60), new PlaceOrderLine(menu.Batata.Id, 40)])));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("invalid_quantity");
        (await Api.CountOrdersAsync()).Should().Be(0);
    }

    // ------------------------------------------------------------------ items_unavailable

    private static async Task<(ProblemResult Problem, JsonElement[] UnavailableItems)> ReadUnavailableAsync(HttpResponseMessage response)
    {
        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(409, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("items_unavailable");
        using var doc = JsonDocument.Parse(problem.RawBody);
        doc.RootElement.TryGetProperty("unavailableItems", out var items).Should().BeTrue(problem.RawBody);
        return (problem, items.EnumerateArray().Select(e => e.Clone()).ToArray());
    }

    private static string? NameOf(JsonElement unavailable) =>
        unavailable.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;

    [Fact]
    public async Task Sold_out_item_returns_409_items_unavailable_with_id_and_name_and_creates_no_order()
    {
        var (owner, menu) = await ArrangeAsync();

        var (_, unavailable) = await ReadUnavailableAsync(await Api.PostOrderAsync(SlugOf(owner),
            new PlaceOrderRequest(null, [new PlaceOrderLine(menu.Caipirinha.Id, 1), new PlaceOrderLine(menu.Chopp.Id, 2)])));

        unavailable.Should().ContainSingle();
        unavailable[0].GetProperty("menuItemId").GetGuid().Should().Be(menu.Chopp.Id);
        NameOf(unavailable[0]).Should().Be("Chopp");
        (await Api.CountOrdersAsync()).Should().Be(0, "nothing is charged nor created when any item is unavailable");
    }

    [Fact]
    public async Task Removed_item_returns_409_without_name()
    {
        var (owner, menu) = await ArrangeAsync();
        (await Api.CreateClient(owner.Token).DeleteAsync($"/api/menu-items/{menu.Batata.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (_, unavailable) = await ReadUnavailableAsync(await Api.PostOrderAsync(SlugOf(owner),
            new PlaceOrderRequest(null, [new PlaceOrderLine(menu.Batata.Id, 1)])));

        unavailable.Should().ContainSingle();
        unavailable[0].GetProperty("menuItemId").GetGuid().Should().Be(menu.Batata.Id);
        NameOf(unavailable[0]).Should().BeNull();
        (await Api.CountOrdersAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Item_of_another_establishment_returns_409_without_leaking_its_name()
    {
        var (ownerA, menuA) = await ArrangeAsync();
        var (_, menuB) = await ArrangeAsync();

        var (_, unavailable) = await ReadUnavailableAsync(await Api.PostOrderAsync(SlugOf(ownerA),
            new PlaceOrderRequest(null, [new PlaceOrderLine(menuA.Batata.Id, 1), new PlaceOrderLine(menuB.Caipirinha.Id, 1)])));

        unavailable.Should().ContainSingle();
        unavailable[0].GetProperty("menuItemId").GetGuid().Should().Be(menuB.Caipirinha.Id);
        NameOf(unavailable[0]).Should().BeNull("names of another tenant's items must not leak");
        (await Api.CountOrdersAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Every_unavailable_item_is_listed()
    {
        var (owner, menu) = await ArrangeAsync();
        var missing = Guid.NewGuid();

        var (_, unavailable) = await ReadUnavailableAsync(await Api.PostOrderAsync(SlugOf(owner),
            new PlaceOrderRequest(null, [new PlaceOrderLine(menu.Chopp.Id, 1), new PlaceOrderLine(missing, 1), new PlaceOrderLine(menu.Batata.Id, 1)])));

        unavailable.Select(u => u.GetProperty("menuItemId").GetGuid()).Should().BeEquivalentTo([menu.Chopp.Id, missing]);
    }

    [Fact]
    public async Task Unknown_slug_returns_404_establishment_not_found()
    {
        var (_, menu) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.PostOrderAsync("nao-existe",
            new PlaceOrderRequest(null, [new PlaceOrderLine(menu.Batata.Id, 1)])));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("establishment_not_found");
        (await Api.CountOrdersAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Owner_token_is_not_required_nor_used()
    {
        var (owner, menu) = await ArrangeAsync();
        var ownerB = await Api.RegisterOwnerAsync();

        var response = await Api.PostOrderAsync(SlugOf(owner),
            new PlaceOrderRequest(null, [new PlaceOrderLine(menu.Batata.Id, 1)]), Api.CreateClient(ownerB.Token));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        (await ApiDriver.ReadSuccessAsync<PublicOrderDto>(response, "order")).Establishment.Slug.Should().Be(SlugOf(owner));
    }

    // ------------------------------------------------------------------ pickup code collision

    /// <summary>Returns the queued codes in order, then falls back to random valid codes.</summary>
    private sealed class ScriptedPickupCodeGenerator(params string[] codes) : IPickupCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);
        private readonly object _lock = new();

        public string Generate()
        {
            lock (_lock)
            {
                return _codes.Count > 0 ? _codes.Dequeue() : EasyMenu.Domain.Orders.PickupCode.Generate();
            }
        }
    }

    [Fact]
    public async Task Pickup_code_collision_with_an_active_order_retries_with_another_code()
    {
        var (owner, menu) = await ArrangeAsync();
        await using var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPickupCodeGenerator>();
            services.AddSingleton<IPickupCodeGenerator>(new ScriptedPickupCodeGenerator("ABCD", "ABCD", "WXYZ"));
        }));
        using var client = host.CreateClient();
        var request = new PlaceOrderRequest(null, [new PlaceOrderLine(menu.Batata.Id, 1)]);

        var first = await ApiDriver.ReadSuccessAsync<PublicOrderDto>(await Api.PostOrderAsync(SlugOf(owner), request, client), "first");
        var second = await ApiDriver.ReadSuccessAsync<PublicOrderDto>(await Api.PostOrderAsync(SlugOf(owner), request, client), "second");

        first.PickupCode.Should().Be("ABCD");
        second.PickupCode.Should().Be("WXYZ", "ABCD is still active (not picked up), so the second order gets the next code");
        (await Api.CountOrdersAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Same_pickup_code_is_allowed_in_different_establishments()
    {
        var (ownerA, menuA) = await ArrangeAsync();
        var (ownerB, menuB) = await ArrangeAsync();
        await using var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPickupCodeGenerator>();
            services.AddSingleton<IPickupCodeGenerator>(new ScriptedPickupCodeGenerator("ABCD", "ABCD"));
        }));
        using var client = host.CreateClient();

        var a = await ApiDriver.ReadSuccessAsync<PublicOrderDto>(await Api.PostOrderAsync(SlugOf(ownerA),
            new PlaceOrderRequest(null, [new PlaceOrderLine(menuA.Batata.Id, 1)]), client), "order A");
        var b = await ApiDriver.ReadSuccessAsync<PublicOrderDto>(await Api.PostOrderAsync(SlugOf(ownerB),
            new PlaceOrderRequest(null, [new PlaceOrderLine(menuB.Batata.Id, 1)]), client), "order B");

        a.PickupCode.Should().Be("ABCD");
        b.PickupCode.Should().Be("ABCD", "uniqueness is per establishment (FR-021)");
    }
}
