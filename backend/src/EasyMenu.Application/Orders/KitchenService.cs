using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Menu;
using EasyMenu.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Orders;

/// <summary>Kitchen panel (FR-025..FR-028): active orders, part transitions and pickup. Tenant-filtered.</summary>
public sealed class KitchenService(IAppDbContext db, TimeProvider timeProvider)
{
    /// <summary>Orders not picked up yet, oldest first.</summary>
    public async Task<IReadOnlyList<KitchenOrderResponse>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Parts)
            .Where(o => o.Status != OrderStatus.PickedUp)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
        return orders.Select(ToKitchenOrder).ToList();
    }

    public Task<KitchenOrderResponse> MovePartAsync(
        Guid orderId, Station station, PartStatusTarget target, CancellationToken cancellationToken) =>
        ChangeAsync(orderId, (order, now) => order.MovePart(station, (PartStatus)target, now), cancellationToken);

    public Task<KitchenOrderResponse> MarkPickedUpAsync(Guid orderId, CancellationToken cancellationToken) =>
        ChangeAsync(orderId, (order, now) => order.MarkPickedUp(now), cancellationToken);

    public static KitchenOrderResponse ToKitchenOrder(Order order) => new()
    {
        Id = order.Id,
        PickupCode = order.PickupCode,
        CustomerName = order.CustomerName,
        Status = order.Status,
        CreatedAt = order.CreatedAt,
        Parts = order.Parts
            .OrderBy(p => p.Station)
            .Select(p => new KitchenOrderPartResponse
            {
                Station = p.Station,
                Status = p.Status,
                Items = order.Items
                    .Where(i => i.Station == p.Station)
                    .Select(i => new KitchenOrderItemResponse { Name = i.NameSnapshot, Quantity = i.Quantity })
                    .ToList(),
            })
            .ToList(),
    };

    // The order row carries the concurrency token (xmin): two concurrent changes of the same order conflict, and
    // the loser is retried once on fresh data, so the derived status always sees every part.
    private async Task<KitchenOrderResponse> ChangeAsync(
        Guid orderId, Action<Order, DateTimeOffset> change, CancellationToken cancellationToken)
    {
        try
        {
            return await ApplyAsync(orderId, change, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return await ApplyAsync(orderId, change, cancellationToken);
        }
    }

    private async Task<KitchenOrderResponse> ApplyAsync(
        Guid orderId, Action<Order, DateTimeOffset> change, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Parts)
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new NotFoundException();

        change(order, timeProvider.GetUtcNowForStorage());
        await db.SaveChangesAsync(cancellationToken);
        return ToKitchenOrder(order);
    }
}
