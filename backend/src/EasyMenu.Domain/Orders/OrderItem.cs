using EasyMenu.Domain.Menu;

namespace EasyMenu.Domain.Orders;

/// <summary>Order line with the name/price snapshot taken at purchase time (constitution IV).</summary>
/// <remarks>Stub created by the test author (T045) so the tests compile; implementation belongs to T049.</remarks>
public sealed class OrderItem
{
    private OrderItem()
    {
    }

    public Guid Id => throw new NotImplementedException();

    public Guid MenuItemId => throw new NotImplementedException();

    public string NameSnapshot => throw new NotImplementedException();

    public long UnitPriceCents => throw new NotImplementedException();

    public int Quantity => throw new NotImplementedException();

    public Station Station => throw new NotImplementedException();

    /// <summary><c>UnitPriceCents × Quantity</c>.</summary>
    public long SubtotalCents => throw new NotImplementedException();
}
