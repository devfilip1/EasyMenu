using System.ComponentModel.DataAnnotations;
using EasyMenu.Application.Menu;
using EasyMenu.Domain.Orders;

namespace EasyMenu.Application.Orders;

// Request/response shapes of contracts/openapi.yaml (PlaceOrderRequest, PlaceOrderLine, PublicOrder, OrderLine,
// UnavailableItem). Only these fields are read: prices or totals sent by the client are ignored (FR-018).

public sealed class PlaceOrderRequest
{
    /// <summary>Optional; trimmed, blank becomes null (FR-016).</summary>
    [StringLength(Order.CustomerNameMaxLength)]
    public string? CustomerName { get; init; }

    [Required]
    public IReadOnlyList<PlaceOrderLineRequest> Items { get; init; } = [];
}

public sealed class PlaceOrderLineRequest
{
    public required Guid MenuItemId { get; init; }

    public required int Quantity { get; init; }
}

public sealed class PublicOrderResponse
{
    public required string PublicToken { get; init; }

    public required string PickupCode { get; init; }

    public string? CustomerName { get; init; }

    public required OrderStatus Status { get; init; }

    public required long TotalCents { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required PublicEstablishmentResponse Establishment { get; init; }

    public required IReadOnlyList<OrderLineResponse> Items { get; init; }
}

public sealed class OrderLineResponse
{
    /// <summary>Name snapshot taken at purchase time.</summary>
    public required string Name { get; init; }

    /// <summary>Price snapshot taken at purchase time.</summary>
    public required long UnitPriceCents { get; init; }

    public required int Quantity { get; init; }

    public required long SubtotalCents { get; init; }
}

public sealed class UnavailableItemResponse
{
    public required Guid MenuItemId { get; init; }

    /// <summary>Current name; absent when the item was removed or belongs to another establishment.</summary>
    public string? Name { get; init; }
}
