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
}
