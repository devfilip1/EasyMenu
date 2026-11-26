using EasyMenu.Domain.Menu;

namespace EasyMenu.Domain.Orders;

/// <summary>Order line with the name/price snapshot taken at purchase time (constitution IV).</summary>
public sealed class OrderItem
{
    private OrderItem(Guid id, Guid menuItemId, string nameSnapshot, long unitPriceCents, int quantity, Station station)
    {
        Id = id;
        MenuItemId = menuItemId;
        NameSnapshot = nameSnapshot;
        UnitPriceCents = unitPriceCents;
        Quantity = quantity;
        Station = station;
        SubtotalCents = unitPriceCents * quantity;
    }

    public Guid Id { get; private set; }

    /// <summary>Logical reference only (no FK): the menu item may be deleted later.</summary>
    public Guid MenuItemId { get; private set; }

    public string NameSnapshot { get; private set; }

    public long UnitPriceCents { get; private set; }

    public int Quantity { get; private set; }

    public Station Station { get; private set; }

    /// <summary><c>UnitPriceCents × Quantity</c>.</summary>
    public long SubtotalCents { get; private set; }

    internal static OrderItem From(OrderLineSnapshot line, int quantity) =>
        new(Guid.CreateVersion7(), line.MenuItemId, line.Name, line.UnitPriceCents, quantity, line.Station);
}
