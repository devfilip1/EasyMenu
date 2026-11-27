using System.ComponentModel.DataAnnotations;
using EasyMenu.Domain.Menu;
using EasyMenu.Domain.Orders;

namespace EasyMenu.Application.Orders;

// Contract shapes KitchenOrder / KitchenOrderPart / KitchenOrderItem / UpdatePartStatusRequest.

/// <summary>Statuses a part can be moved to; <c>received</c> is only the initial state (400 if sent).</summary>
public enum PartStatusTarget
{
    InPreparation = PartStatus.InPreparation,
    Ready = PartStatus.Ready,
}

/// <summary>Code typed by the staff to confirm a pickup (FR-027a); the server trims it and ignores case.</summary>
public sealed class ConfirmPickupRequest
{
    // Present but blank is a business error (pickup_code_required) checked after the order state, so empty strings
    // pass the model validation; a missing property is 400 validation_failed.
    [Required(AllowEmptyStrings = true)]
    [StringLength(MaxLength)]
    public string PickupCode { get; init; } = null!;

    public const int MaxLength = 20;
}

public sealed class UpdatePartStatusRequest
{
    public required PartStatusTarget Status { get; init; }
}

public sealed class KitchenOrderResponse
{
    public required Guid Id { get; init; }

    public required string PickupCode { get; init; }

    public string? CustomerName { get; init; }

    public required OrderStatus Status { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>One per station present in the order, each with only its station's items.</summary>
    public required IReadOnlyList<KitchenOrderPartResponse> Parts { get; init; }
}

public sealed class KitchenOrderPartResponse
{
    public required Station Station { get; init; }

    public required PartStatus Status { get; init; }

    public required IReadOnlyList<KitchenOrderItemResponse> Items { get; init; }
}

public sealed class KitchenOrderItemResponse
{
    public required string Name { get; init; }

    public required int Quantity { get; init; }
}
