using EasyMenu.Domain.Menu;
using EasyMenu.Domain.Orders;

namespace EasyMenu.Application.Orders;

// Response shapes of contracts/openapi.yaml for the owner's order history (FR-035): OrderSummaryPage,
// OrderSummary, OrderDetail, OrderDetailLine and OrderPartStatus.

public sealed class OrderSummaryPageResponse
{
    public required IReadOnlyList<OrderSummaryResponse> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }
}

public sealed class OrderSummaryResponse
{
    public required Guid Id { get; init; }

    public required string PickupCode { get; init; }

    public string? CustomerName { get; init; }

    public required OrderStatus Status { get; init; }

    public required long TotalCents { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class OrderDetailResponse
{
    public required Guid Id { get; init; }

    public required string PickupCode { get; init; }

    public string? CustomerName { get; init; }

    public required OrderStatus Status { get; init; }

    public required long TotalCents { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? PickedUpAt { get; init; }

    public required IReadOnlyList<OrderDetailLineResponse> Items { get; init; }

    public required IReadOnlyList<OrderPartStatusResponse> Parts { get; init; }
}

public sealed class OrderDetailLineResponse
{
    /// <summary>Name snapshot taken at purchase time.</summary>
    public required string Name { get; init; }

    /// <summary>Price snapshot taken at purchase time.</summary>
    public required long UnitPriceCents { get; init; }

    public required int Quantity { get; init; }

    public required long SubtotalCents { get; init; }

    public required Station Station { get; init; }
}

public sealed class OrderPartStatusResponse
{
    public required Station Station { get; init; }

    public required PartStatus Status { get; init; }
}
