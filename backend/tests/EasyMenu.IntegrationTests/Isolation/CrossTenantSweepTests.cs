using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using EasyMenu.IntegrationTests.Contract;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Isolation;

/// <summary>
/// SC-005 / constitution III sweep. The operations come from contracts/openapi.yaml, so every authenticated
/// operation that takes an id is covered automatically (including future ones): with A's ids and B's token the
/// answer is 404 <c>not_found</c> (or 403 when B's role cannot use the endpoint), A's data stays intact, and no
/// listing of B contains any id of A.
/// </summary>
public sealed class CrossTenantSweepTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly string[] HttpMethods = ["get", "put", "post", "delete", "patch"];

    private static readonly JsonObject Contract = OpenApiDocuments.LoadContract();

    private static IEnumerable<(string Method, string Path)> AuthenticatedOperations()
    {
        foreach (var (path, item) in Contract["paths"]!.AsObject())
        {
            foreach (var method in HttpMethods)
            {
                if (item![method] is JsonObject operation && !(operation["security"] is JsonArray { Count: 0 }))
                {
                    yield return (method.ToUpperInvariant(), path);
                }
            }
        }
    }

    public static TheoryData<string, string, string> ByIdOperations()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var (method, path) in AuthenticatedOperations().Where(o => o.Path.Contains('{')))
        {
            data.Add(method, path, "owner");
            data.Add(method, path, "kitchen");
        }

        return data;
    }

    public static TheoryData<string, string> ListOperations()
    {
        var data = new TheoryData<string, string>();
        foreach (var (method, path) in AuthenticatedOperations().Where(o => o.Method == "GET" && !o.Path.Contains('{')))
        {
            data.Add(path, "owner");
            data.Add(path, "kitchen");
        }

        return data;
    }

    // ------------------------------------------------------------------ scenario

    private sealed record Tenant(
        RegisteredOwner Owner,
        string KitchenToken,
        DefaultMenu Menu,
        StaffMemberDto Staff,
        KitchenOrderDto Order,
        PublicOrderDto PublicOrder)
    {
        public IEnumerable<Guid> Ids =>
        [
            Owner.EstablishmentId, Owner.UserId, Staff.Id, Order.Id,
            Menu.Drinks.Id, Menu.Porcoes.Id, Menu.Caipirinha.Id, Menu.Chopp.Id, Menu.Batata.Id,
        ];
    }

    private async Task<Tenant> ArrangeTenantAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var (staff, password) = await Api.CreateStaffAsync(owner.Token);
        var kitchenToken = (await Api.LoginAsync(staff.Email, password)).AccessToken;
        var (placed, order) = await Api.PlaceKitchenOrderAsync(owner, "Cliente",
            new PlaceOrderLine(menu.Caipirinha.Id, 1), new PlaceOrderLine(menu.Batata.Id, 1));
        return new Tenant(owner, kitchenToken, menu, staff, order, placed);
    }

    /// <summary>Everything A can see, to prove B's attempts changed nothing.</summary>
    private async Task<string> SnapshotAsync(Tenant a)
    {
        var owner = Api.CreateClient(a.Owner.Token);
        var parts = new List<string>();
        foreach (var route in new[] { "/api/establishment", "/api/categories", "/api/menu-items", "/api/staff", "/api/kitchen/orders", "/api/orders" })
        {
            parts.Add(await (await owner.GetAsync(route)).Content.ReadAsStringAsync());
        }

        return string.Join("\n", parts);
    }

    /// <summary>Path with A's ids; fails explicitly when the contract gains a parameter this sweep does not know.</summary>
    private static string ResolvePath(string path, Tenant a)
    {
        var resolved = path
            .Replace("/api/categories/{id}", $"/api/categories/{a.Menu.Drinks.Id}")
            .Replace("/api/menu-items/{id}", $"/api/menu-items/{a.Menu.Batata.Id}")
            .Replace("/api/staff/{id}", $"/api/staff/{a.Staff.Id}")
            .Replace("{orderId}", a.Order.Id.ToString())
            .Replace("{station}", "bar");

        resolved.Should().NotContain("{", "CrossTenantSweepTests must learn how to fill the parameters of {0}", path);
        return resolved;
    }

    /// <summary>A body that is valid for B (so validation never answers before the tenant check).</summary>
    private static HttpContent? BodyFor(string method, string path, Tenant a, Tenant b)
    {
        if (method is not ("PUT" or "POST" or "PATCH"))
        {
            return null;
        }

        object? body = path switch
        {
            "/api/categories/{id}" => new CategoryRequest("Hackeada"),
            "/api/categories/{id}/items/order" => new ReorderRequest([a.Menu.Caipirinha.Id, a.Menu.Chopp.Id]),
            "/api/menu-items/{id}" => new MenuItemRequest(b.Menu.Drinks.Id, "Hackeado", null, 1, null, true, "bar"),
            "/api/menu-items/{id}/availability" => new AvailabilityRequest(false),
            "/api/kitchen/orders/{orderId}/parts/{station}/status" => new UpdatePartStatusRequest("ready"),
            // The right code of A's order: only the tenant check may refuse it.
            "/api/kitchen/orders/{orderId}/pickup" => new ConfirmPickupRequest(a.Order.PickupCode),
            _ => null,
        };

        if (path == "/api/menu-items/{id}/photo")
        {
            var file = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9]);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return new MultipartFormDataContent { { file, "file", "a.jpg" } };
        }

        return body is null ? null : JsonContent.Create(body, body.GetType(), options: ApiDriver.Json);
    }

    /// <summary>Kitchen may only use the kitchen panel (and /api/auth/me); every other authenticated route is owner-only.</summary>
    private static bool KitchenMayUse(string path) => path.StartsWith("/api/kitchen/", StringComparison.Ordinal) || path == "/api/auth/me";

    // ------------------------------------------------------------------ tests

    [Theory]
    [MemberData(nameof(ByIdOperations))]
    public async Task Ids_of_a_with_token_of_b_give_404_or_403_and_change_nothing(string method, string path, string principal)
    {
        var a = await ArrangeTenantAsync();
        var b = await ArrangeTenantAsync();
        var before = await SnapshotAsync(a);
        var token = principal == "owner" ? b.Owner.Token : b.KitchenToken;
        using var request = new HttpRequestMessage(new HttpMethod(method), ResolvePath(path, a)) { Content = BodyFor(method, path, a, b) };

        var response = await Api.CreateClient(token).SendAsync(request);

        var problem = await ApiDriver.ReadProblemAsync(response);
        var expected = principal == "kitchen" && !KitchenMayUse(path) ? 403 : 404;
        problem.Status.Should().Be(expected, "{0} {1} as {2} of B: {3}", method, path, principal, problem.RawBody);
        if (expected == 404)
        {
            problem.MediaType.Should().Be("application/problem+json", "a 404 from the tenant check, not a missing route");
            problem.Code.Should().Be("not_found");
        }

        (await SnapshotAsync(a)).Should().Be(before, "B must not change anything of A through {0} {1}", method, path);
    }

    [Theory]
    [MemberData(nameof(ListOperations))]
    public async Task Listings_of_b_never_contain_ids_of_a(string path, string principal)
    {
        var a = await ArrangeTenantAsync();
        var b = await ArrangeTenantAsync();
        var token = principal == "owner" ? b.Owner.Token : b.KitchenToken;

        var response = await Api.CreateClient(token).GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        if (principal == "kitchen" && !KitchenMayUse(path))
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} is owner-only: {1}", path, body);
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK, "{0} as {1} of B: {2}", path, principal, body);
        foreach (var id in a.Ids)
        {
            body.Should().NotContainEquivalentOf(id.ToString(), "{0} of B must not expose ids of A", path);
        }

        body.Should().NotContain(a.PublicOrder.PublicToken, "{0} of B must not expose A's order token", path);
    }

    [Fact]
    public void Sweep_covers_every_authenticated_operation_with_an_id_in_the_contract()
    {
        var byId = ByIdOperations().Count;
        var listings = ListOperations().Count;

        // Sanity: the MVP contract has 13 authenticated by-id operations and 7 authenticated listings (× 2 principals).
        byId.Should().BeGreaterThanOrEqualTo(2 * 13);
        listings.Should().BeGreaterThanOrEqualTo(2 * 7);
    }
}
