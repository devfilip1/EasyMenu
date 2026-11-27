using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Menu;
using EasyMenu.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Orders;

/// <summary>Kitchen panel (FR-025..FR-028): active orders, part transitions and pickup. Tenant-filtered.</summary>
public sealed class KitchenService(IAppDbContext db, IOrderNotifier notifier, TimeProvider timeProvider)
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

    /// <summary>Confirms the pickup with the typed code; a concurrent loser is re-evaluated and gets order_already_picked_up.</summary>
    public Task<KitchenOrderResponse> ConfirmPickupAsync(Guid orderId, string pickupCode, CancellationToken cancellationToken) =>
        ChangeAsync(orderId, (order, now) => order.ConfirmPickup(pickupCode, now), cancellationToken);

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
        Applied applied;
        try
        {
            applied = await ApplyAsync(orderId, change, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            applied = await ApplyAsync(orderId, change, cancellationToken);
        }

        // After the commit, and only when something changed (repeating the current status is silent).
        if (applied.Changed)
        {
            await notifier.OrderChangedAsync(applied.Order, applied.PreviousStatus, CancellationToken.None);
        }

        return ToKitchenOrder(applied.Order);
    }

    private async Task<Applied> ApplyAsync(Guid orderId, Action<Order, DateTimeOffset> change, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Parts)
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new NotFoundException();

        var previousStatus = order.Status;
        var previousParts = PartStatuses(order);
        change(order, timeProvider.GetUtcNowForStorage());
        await db.SaveChangesAsync(cancellationToken);

        var changed = order.Status != previousStatus || !PartStatuses(order).SequenceEqual(previousParts);
        return new Applied(order, previousStatus, changed);
    }

    private static List<(Station, PartStatus)> PartStatuses(Order order) =>
        order.Parts.OrderBy(p => p.Station).Select(p => (p.Station, p.Status)).ToList();

    private sealed record Applied(Order Order, OrderStatus PreviousStatus, bool Changed);
}
