using System.Net.Http.Json;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>Scenario helpers for US1 (establishment, categories, menu items).</summary>
public sealed partial class ApiDriver
{
    /// <summary>Creates a Kitchen user in the establishment and returns its bearer token.</summary>
    public async Task<string> CreateKitchenTokenAsync(Guid establishmentId)
    {
        var kitchen = await CreateKitchenUserAsync(establishmentId);
        return (await LoginAsync(kitchen.Email, kitchen.Password)).AccessToken;
    }

    // ------------------------------------------------------------------ categories

    /// <summary><c>POST /api/categories</c> (fails the test if not 2xx).</summary>
    public async Task<CategoryDto> CreateCategoryAsync(string token, string name) =>
        await ReadSuccessAsync<CategoryDto>(
            await CreateClient(token).PostAsJsonAsync("/api/categories", new CategoryRequest(name), Json),
            $"create category '{name}'");

    /// <summary><c>GET /api/categories</c>.</summary>
    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(string token) =>
        await ReadSuccessAsync<List<CategoryDto>>(await CreateClient(token).GetAsync("/api/categories"), "list categories");

    // ------------------------------------------------------------------ menu items

    /// <summary><c>POST /api/menu-items</c> (fails the test if not 2xx).</summary>
    public async Task<MenuItemDto> CreateMenuItemAsync(
        string token,
        Guid categoryId,
        string name,
        long priceCents,
        long? promotionalPriceCents = null,
        string station = "kitchen",
        bool isAvailable = true,
        string? description = null) =>
        await ReadSuccessAsync<MenuItemDto>(
            await CreateClient(token).PostAsJsonAsync("/api/menu-items",
                new MenuItemRequest(categoryId, name, description, priceCents, promotionalPriceCents, isAvailable, station), Json),
            $"create menu item '{name}'");

    /// <summary><c>GET /api/menu-items[?categoryId=]</c>.</summary>
    public async Task<IReadOnlyList<MenuItemDto>> ListMenuItemsAsync(string token, Guid? categoryId = null) =>
        await ReadSuccessAsync<List<MenuItemDto>>(
            await CreateClient(token).GetAsync(categoryId is null ? "/api/menu-items" : $"/api/menu-items?categoryId={categoryId}"),
            "list menu items");

    /// <summary>
    /// Default menu: categories Drinks and Porções; items Caipirinha (2500, promo 1990, bar), Chopp (1200, bar,
    /// sold out) and Batata (3000, kitchen).
    /// </summary>
    public async Task<DefaultMenu> CreateMenuAsync(string token)
    {
        var drinks = await CreateCategoryAsync(token, "Drinks");
        var porcoes = await CreateCategoryAsync(token, "Porções");
        var caipirinha = await CreateMenuItemAsync(token, drinks.Id, "Caipirinha", 2500, promotionalPriceCents: 1990, station: "bar");
        var chopp = await CreateMenuItemAsync(token, drinks.Id, "Chopp", 1200, station: "bar", isAvailable: false);
        var batata = await CreateMenuItemAsync(token, porcoes.Id, "Batata", 3000, station: "kitchen");
        return new DefaultMenu(drinks, porcoes, caipirinha, chopp, batata);
    }
}

public sealed record UpdateEstablishmentRequest(string Name);

public sealed record CategoryRequest(string Name);

public sealed record ReorderRequest(IReadOnlyList<Guid> Ids);

public sealed record CategoryDto(Guid Id, string Name, int Position, int ItemCount);

public sealed record MenuItemRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    long PriceCents,
    long? PromotionalPriceCents,
    bool IsAvailable,
    string Station);

public sealed record AvailabilityRequest(bool IsAvailable);

public sealed record MenuItemDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    string? PhotoUrl,
    long PriceCents,
    long? PromotionalPriceCents,
    long EffectivePriceCents,
    bool IsAvailable,
    string Station,
    int Position);

public sealed record DefaultMenu(
    CategoryDto Drinks,
    CategoryDto Porcoes,
    MenuItemDto Caipirinha,
    MenuItemDto Chopp,
    MenuItemDto Batata);
