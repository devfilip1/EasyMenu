using EasyMenu.Domain.Menu;

namespace EasyMenu.Domain.Orders;

/// <summary>The part of an order prepared by one station.</summary>
/// <remarks>Stub created by the test author (T045) so the tests compile; implementation belongs to T049.</remarks>
public sealed class OrderPart
{
    private OrderPart()
    {
    }

    public Guid Id => throw new NotImplementedException();

    public Station Station => throw new NotImplementedException();

    public PartStatus Status => throw new NotImplementedException();

    public DateTimeOffset UpdatedAt => throw new NotImplementedException();
}
