using EasyMenu.Application.Orders;

namespace EasyMenu.Api.ErrorHandling;

/// <summary>Documentation shape of the 409 <c>items_unavailable</c> response (contract <c>ItemsUnavailableProblem</c>).</summary>
public sealed class ItemsUnavailableProblem
{
    public const string UnavailableItemsExtension = "unavailableItems";

    public string? Type { get; init; }

    public string? Title { get; init; }

    public required int Status { get; init; }

    public string? Detail { get; init; }

    public required ErrorCode Code { get; init; }

    public IDictionary<string, string[]>? Errors { get; init; }

    public required IReadOnlyList<UnavailableItemResponse> UnavailableItems { get; init; }
}
