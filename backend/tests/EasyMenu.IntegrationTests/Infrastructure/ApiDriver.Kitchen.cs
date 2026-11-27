using System.Net.Http.Json;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>Scenario helpers for US3 (kitchen panel).</summary>
public sealed partial class ApiDriver
{
    /// <summary><c>GET /api/kitchen/orders</c> (fails the test if not 2xx).</summary>
    public async Task<IReadOnlyList<KitchenOrderDto>> ListKitchenOrdersAsync(string token) =>
        await ReadSuccessAsync<List<KitchenOrderDto>>(await CreateClient(token).GetAsync("/api/kitchen/orders"), "kitchen orders");

    /// <summary><c>PUT /api/kitchen/orders/{orderId}/parts/{station}/status</c> (raw response).</summary>
    public Task<HttpResponseMessage> PutPartStatusAsync(string? token, Guid orderId, string station, string status) =>
        CreateClient(token).PutAsJsonAsync(
            $"/api/kitchen/orders/{orderId}/parts/{station}/status", new UpdatePartStatusRequest(status), Json);

    /// <summary><c>PUT /api/kitchen/orders/{orderId}/pickup</c> with <c>{ pickupCode }</c> (raw response).</summary>
    public Task<HttpResponseMessage> PutPickupAsync(string? token, Guid orderId, string? pickupCode) =>
        PutPickupRawAsync(token, orderId, JsonContent.Create(new ConfirmPickupRequest(pickupCode), options: Json));

    /// <summary>Confirms the pickup of <paramref name="order"/> with its own code (raw response).</summary>
    public Task<HttpResponseMessage> PutPickupAsync(string? token, KitchenOrderDto order) =>
        PutPickupAsync(token, order.Id, order.PickupCode);

    /// <summary><c>PUT /api/kitchen/orders/{orderId}/pickup</c> with an arbitrary body (raw response).</summary>
    public Task<HttpResponseMessage> PutPickupRawAsync(string? token, Guid orderId, HttpContent? content) =>
        CreateClient(token).PutAsync($"/api/kitchen/orders/{orderId}/pickup", content);

    /// <summary>Places an order and returns it as seen by the kitchen panel (matched by pickup code).</summary>
    public async Task<(PublicOrderDto Public, KitchenOrderDto Kitchen)> PlaceKitchenOrderAsync(
        RegisteredOwner owner, string? customerName, params PlaceOrderLine[] lines)
    {
        var placed = await PlaceOrderAsync(owner.Response.User.Establishment.Slug, customerName, lines);
        var kitchen = (await ListKitchenOrdersAsync(owner.Token)).Single(o => o.PickupCode == placed.PickupCode);
        return (placed, kitchen);
    }

    /// <summary>Moves every part of the order to <c>ready</c> (fails the test on any non-2xx).</summary>
    public async Task<KitchenOrderDto> MakeReadyAsync(string token, KitchenOrderDto order)
    {
        var last = order;
        foreach (var part in order.Parts)
        {
            last = await ReadSuccessAsync<KitchenOrderDto>(
                await PutPartStatusAsync(token, order.Id, part.Station, "ready"), $"{part.Station} ready");
        }

        return last;
    }
}

public sealed record UpdatePartStatusRequest(string Status);

public sealed record ConfirmPickupRequest(string? PickupCode);

public sealed record KitchenOrderItemDto(string Name, int Quantity);

public sealed record KitchenOrderPartDto(string Station, string Status, IReadOnlyList<KitchenOrderItemDto> Items);

public sealed record KitchenOrderDto(
    Guid Id,
    string PickupCode,
    string? CustomerName,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<KitchenOrderPartDto> Parts);

public sealed record OrderPickedUpEventDto(Guid Id, string PickupCode);
