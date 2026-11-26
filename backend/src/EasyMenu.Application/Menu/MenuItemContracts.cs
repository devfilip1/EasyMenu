using System.ComponentModel.DataAnnotations;
using EasyMenu.Domain.Menu;

namespace EasyMenu.Application.Menu;

// Request/response shapes of contracts/openapi.yaml (MenuItem, MenuItemRequest, AvailabilityRequest).
// Value-type fields use the C# `required` modifier, so a missing field is a 400 instead of a silent default.

public sealed class MenuItemRequest
{
    public required Guid CategoryId { get; init; }

    [Required]
    [StringLength(MenuItem.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(MenuItem.DescriptionMaxLength)]
    public string? Description { get; init; }

    public required long PriceCents { get; init; }

    public long? PromotionalPriceCents { get; init; }

    public required bool IsAvailable { get; init; }

    public required Station Station { get; init; }
}

public sealed class AvailabilityRequest
{
    public required bool IsAvailable { get; init; }
}

public sealed class MenuItemResponse
{
    public required Guid Id { get; init; }

    public required Guid CategoryId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Relative to the API root (<c>/media/...</c>); null = default image.</summary>
    public string? PhotoUrl { get; init; }

    public required long PriceCents { get; init; }

    public long? PromotionalPriceCents { get; init; }

    /// <summary><c>PromotionalPriceCents ?? PriceCents</c>.</summary>
    public required long EffectivePriceCents { get; init; }

    public required bool IsAvailable { get; init; }

    public required Station Station { get; init; }

    public required int Position { get; init; }
}
