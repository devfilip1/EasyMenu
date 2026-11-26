using System.Net.Http.Json;
using EasyMenu.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>Scenario helpers for US2 (public menu, checkout, public order page).</summary>
public sealed partial class ApiDriver
{
    /// <summary><c>GET /api/public/establishments/{slug}/menu</c> without token (raw response).</summary>
    public Task<HttpResponseMessage> GetPublicMenuResponseAsync(string slug, HttpClient? client = null) =>
        (client ?? _anonymous).GetAsync($"/api/public/establishments/{slug}/menu");

    /// <summary>Public menu (fails the test if not 2xx).</summary>
    public async Task<PublicMenuDto> GetPublicMenuAsync(string slug) =>
        await ReadSuccessAsync<PublicMenuDto>(await GetPublicMenuResponseAsync(slug), $"public menu of '{slug}'");

    /// <summary><c>POST /api/public/establishments/{slug}/orders</c> without token (raw response).</summary>
    public Task<HttpResponseMessage> PostOrderAsync(string slug, object request, HttpClient? client = null) =>
        (client ?? _anonymous).PostAsync($"/api/public/establishments/{slug}/orders",
            JsonContent.Create(request, request.GetType(), options: Json));

    /// <summary>Places an order (fails the test if not 2xx).</summary>
    public async Task<PublicOrderDto> PlaceOrderAsync(string slug, string? customerName, params PlaceOrderLine[] lines) =>
        await ReadSuccessAsync<PublicOrderDto>(
            await PostOrderAsync(slug, new PlaceOrderRequest(customerName, lines)), $"place order at '{slug}'");

    /// <summary><c>GET /api/public/orders/{publicToken}</c> without token (raw response).</summary>
    public Task<HttpResponseMessage> GetPublicOrderResponseAsync(string publicToken) =>
        _anonymous.GetAsync($"/api/public/orders/{publicToken}");

    /// <summary>Counts orders of every tenant directly in the database.</summary>
    public async Task<int> CountOrdersAsync()
    {
        var count = 0;
        await WithDbAsync(async db => count = await db.Set<Order>().IgnoreQueryFilters().CountAsync());
        return count;
    }
}

public sealed record PublicEstablishmentDto(string Name, string Slug);

public sealed record PublicMenuItemDto(
    Guid Id,
    string Name,
    string? Description,
    string? PhotoUrl,
    long PriceCents,
    long? PromotionalPriceCents,
    long EffectivePriceCents,
    bool IsAvailable);

public sealed record PublicCategoryDto(Guid Id, string Name, IReadOnlyList<PublicMenuItemDto> Items);

public sealed record PublicMenuDto(PublicEstablishmentDto Establishment, IReadOnlyList<PublicCategoryDto> Categories);

public sealed record PlaceOrderLine(Guid MenuItemId, int Quantity);

public sealed record PlaceOrderRequest(string? CustomerName, IReadOnlyList<PlaceOrderLine> Items);

public sealed record OrderLineDto(string Name, long UnitPriceCents, int Quantity, long SubtotalCents);

public sealed record PublicOrderDto(
    string PublicToken,
    string PickupCode,
    string? CustomerName,
    string Status,
    long TotalCents,
    DateTimeOffset CreatedAt,
    PublicEstablishmentDto Establishment,
    IReadOnlyList<OrderLineDto> Items);
