using EasyMenu.Application.Menu;
using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Orders;

namespace EasyMenu.Application.Orders;

public static class OrderMappings
{
    public static PublicOrderResponse ToPublicOrder(Order order, Establishment establishment) => new()
    {
        PublicToken = order.PublicToken,
        PickupCode = order.PickupCode,
        CustomerName = order.CustomerName,
        Status = order.Status,
        TotalCents = order.TotalCents,
        CreatedAt = order.CreatedAt,
        Establishment = new PublicEstablishmentResponse { Name = establishment.Name, Slug = establishment.Slug },
        Items = order.Items.Select(ToLine).ToList(),
    };

    public static OrderLineResponse ToLine(OrderItem item) => new()
    {
        Name = item.NameSnapshot,
        UnitPriceCents = item.UnitPriceCents,
        Quantity = item.Quantity,
        SubtotalCents = item.SubtotalCents,
    };
}
