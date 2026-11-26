namespace EasyMenu.Domain.Orders;

/// <summary>A paid order of an establishment (data-model "Order").</summary>
/// <remarks>Stub created by the test author (T045) so the tests compile; implementation belongs to T049.</remarks>
public sealed class Order
{
    private Order()
    {
    }

    public Guid Id => throw new NotImplementedException();

    public Guid EstablishmentId => throw new NotImplementedException();

    public string PublicToken => throw new NotImplementedException();

    public string PickupCode => throw new NotImplementedException();

    public string? CustomerName => throw new NotImplementedException();

    public OrderStatus Status => throw new NotImplementedException();

    /// <summary>Σ <see cref="OrderItem.SubtotalCents"/>, always computed by the domain (FR-018).</summary>
    public long TotalCents => throw new NotImplementedException();

    public DateTimeOffset CreatedAt => throw new NotImplementedException();

    public DateTimeOffset? PickedUpAt => throw new NotImplementedException();

    public IReadOnlyList<OrderItem> Items => throw new NotImplementedException();

    public IReadOnlyList<OrderPart> Parts => throw new NotImplementedException();

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
        DateTimeOffset now) => throw new NotImplementedException();

    /// <summary>
    /// Moves the part of <paramref name="station"/> to <paramref name="target"/> (FR-025): same status is a no-op,
    /// going back or changing a picked-up order gives <c>invalid_status_transition</c>, a missing station gives
    /// <c>not_found</c>. Recalculates <see cref="Status"/> afterwards.
    /// </summary>
    /// <remarks>Stub signature added by the test author (T059); implementation belongs to T061.</remarks>
    public void MovePart(Menu.Station station, PartStatus target, DateTimeOffset now) => throw new NotImplementedException();

    /// <summary>Derives <see cref="Status"/> from the parts and <see cref="PickedUpAt"/> (data-model table, FR-026).</summary>
    /// <remarks>Stub signature added by the test author (T059); implementation belongs to T061.</remarks>
    public void RecalculateStatus() => throw new NotImplementedException();

    /// <summary>Marks the order as picked up; only from <c>ready</c> (<c>order_not_ready</c>), idempotent once picked up.</summary>
    /// <remarks>Stub signature added by the test author (T059); implementation belongs to T061.</remarks>
    public void MarkPickedUp(DateTimeOffset now) => throw new NotImplementedException();
}
