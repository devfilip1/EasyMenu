using EasyMenu.Domain.Common;

namespace EasyMenu.Domain.Orders;

/// <summary>A paid order of an establishment (data-model "Order").</summary>
public sealed class Order : ITenantOwned
{
    public const int CustomerNameMaxLength = 60;
    public const int MinQuantity = 1;
    public const int MaxQuantity = 99;
    public const string InvalidStatusTransition = "invalid_status_transition";

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderPart> _parts = [];

    private Order(Guid id, Guid establishmentId, string publicToken, string pickupCode, string? customerName,
        OrderStatus status, long totalCents, DateTimeOffset createdAt)
    {
        Id = id;
        EstablishmentId = establishmentId;
        PublicToken = publicToken;
        PickupCode = pickupCode;
        CustomerName = customerName;
        Status = status;
        TotalCents = totalCents;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid EstablishmentId { get; private set; }

    public string PublicToken { get; private set; }

    public string PickupCode { get; private set; }

    public string? CustomerName { get; private set; }

    public OrderStatus Status { get; private set; }

    /// <summary>Σ <see cref="OrderItem.SubtotalCents"/>, always computed by the domain (FR-018).</summary>
    public long TotalCents { get; private set; }

    /// <summary>Returned by the payment gateway (simulated in the MVP).</summary>
    public string? PaymentTransactionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PickedUpAt { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<OrderPart> Parts => _parts;

    /// <summary>
    /// Creates a paid order: rejects an empty cart (<c>empty_cart</c>) and quantities outside 1..99
    /// (<c>invalid_quantity</c>, also for the grouped sum), groups repeated lines, snapshots name/price/station,
    /// creates one <c>received</c> part per station present and computes the total.
    /// </summary>
    public static Order Place(
        Guid establishmentId,
        string? customerName,
        IReadOnlyCollection<OrderLineSnapshot> lines,
        string pickupCode,
        string publicToken,
        DateTimeOffset now)
    {
        if (lines.Count == 0)
        {
            throw new DomainException("empty_cart");
        }

        // Every line is validated on its own before grouping: 3 + 0 is not a valid 3.
        if (lines.Any(l => !IsValidQuantity(l.Quantity)))
        {
            throw new DomainException("invalid_quantity");
        }

        var items = lines
            .GroupBy(l => l.MenuItemId)
            .Select(g => OrderItem.From(g.First(), g.Sum(l => l.Quantity)))
            .ToList();
        if (items.Any(i => !IsValidQuantity(i.Quantity)))
        {
            throw new DomainException("invalid_quantity");
        }

        var order = new Order(Guid.CreateVersion7(), establishmentId, publicToken, pickupCode, ValidCustomerName(customerName),
            OrderStatus.Paid, items.Sum(i => i.SubtotalCents), now);
        order._items.AddRange(items);
        order._parts.AddRange(items.Select(i => i.Station).Distinct().Order().Select(s => OrderPart.Received(s, now)));
        return order;
    }

    /// <summary>Records the payment of the order (simulated gateway in the MVP).</summary>
    public void RecordPayment(string transactionId) => PaymentTransactionId = transactionId;

    /// <summary>A new pickup code/token after a uniqueness collision, before the order is saved.</summary>
    public void ReassignIdentifiers(string pickupCode, string publicToken)
    {
        PickupCode = pickupCode;
        PublicToken = publicToken;
    }

    /// <summary>
    /// Moves the part of <paramref name="station"/> to <paramref name="target"/> (FR-025): same status is a no-op,
    /// going back or changing a picked-up order gives <c>invalid_status_transition</c>, a missing station gives
    /// <c>not_found</c>. Recalculates <see cref="Status"/> afterwards.
    /// </summary>
    public void MovePart(Menu.Station station, PartStatus target, DateTimeOffset now)
    {
        if (Status == OrderStatus.PickedUp)
        {
            throw new DomainException(InvalidStatusTransition);
        }

        var part = _parts.SingleOrDefault(p => p.Station == station) ?? throw new DomainException("not_found");
        part.MoveTo(target, now);
        RecalculateStatus();
    }

    /// <summary>Derives <see cref="Status"/> from the parts and <see cref="PickedUpAt"/> (data-model table, FR-026).</summary>
    public void RecalculateStatus() => Status = PickedUpAt is not null ? OrderStatus.PickedUp
        : _parts.All(p => p.Status == PartStatus.Ready) ? OrderStatus.Ready
        : _parts.Any(p => p.Status != PartStatus.Received) ? OrderStatus.InPreparation
        : OrderStatus.Paid;

    /// <summary>Marks the order as picked up; only from <c>ready</c> (<c>order_not_ready</c>), idempotent once picked up.</summary>
    public void MarkPickedUp(DateTimeOffset now)
    {
        if (Status == OrderStatus.PickedUp)
        {
            return; // keeps the first pickup time
        }

        if (Status != OrderStatus.Ready)
        {
            throw new DomainException("order_not_ready");
        }

        PickedUpAt = now;
        RecalculateStatus();
    }

    /// <summary>
    /// Confirms the pickup with the code typed by the staff (FR-027a): already picked up → <c>order_already_picked_up</c>;
    /// not ready → <c>order_not_ready</c>; blank → <c>pickup_code_required</c>; different (after trim, case-insensitive)
    /// → <c>invalid_pickup_code</c>. Replaces <see cref="MarkPickedUp"/>.
    /// </summary>
    /// <remarks>Stub signature added by the test author (T152); implementation belongs to T153.</remarks>
    public void ConfirmPickup(string? providedCode, DateTimeOffset now) => throw new NotImplementedException();

    private static bool IsValidQuantity(int quantity) => quantity is >= MinQuantity and <= MaxQuantity;

    private static string? ValidCustomerName(string? customerName)
    {
        var trimmed = customerName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= CustomerNameMaxLength ? trimmed : throw new DomainException("validation_failed");
    }
}
