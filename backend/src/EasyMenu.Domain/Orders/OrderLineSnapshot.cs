using EasyMenu.Domain.Menu;

namespace EasyMenu.Domain.Orders;

/// <summary>
/// A cart line as seen at purchase time: the menu item's current name, effective price and station
/// (snapshot), plus the requested quantity. Input of <see cref="Order.Place"/>.
/// </summary>
public sealed record OrderLineSnapshot(Guid MenuItemId, string Name, long UnitPriceCents, Station Station, int Quantity);
