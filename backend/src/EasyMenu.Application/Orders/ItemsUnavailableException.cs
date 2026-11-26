using EasyMenu.Domain.Common;

namespace EasyMenu.Application.Orders;

/// <summary>409 <c>items_unavailable</c>: the cart has sold-out, removed, foreign or unknown items (FR-020).</summary>
public sealed class ItemsUnavailableException(IReadOnlyList<UnavailableItemResponse> items) : DomainException(ErrorCode)
{
    public const string ErrorCode = "items_unavailable";

    public IReadOnlyList<UnavailableItemResponse> Items { get; } = items;
}
