using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Common;
using EasyMenu.Application.Menu;
using EasyMenu.Domain.Common;
using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Orders;

/// <summary>
/// Anonymous checkout and order page (FR-016..FR-021, FR-029, FR-031a). There is no tenant: the tenant filter is
/// bypassed explicitly and every query is scoped by the slug's establishment or by the order's public token.
/// </summary>
public sealed class CheckoutService(
    IAppDbContext db,
    IPickupCodeGenerator pickupCodes,
    IPaymentGateway paymentGateway,
    IOrderNotifier notifier,
    TimeProvider timeProvider)
{
    public const string OrderNotFound = "order_not_found";
    public const string PaymentDeclined = "payment_declined";

    /// <summary>Unique indexes that a fresh pickup code / public token can satisfy on retry (research R17).</summary>
    public const string PickupCodeIndex = "ix_orders_establishment_id_pickup_code";
    public const string PublicTokenIndex = "ix_orders_public_token";

    private const int MaxIdentifierAttempts = 5;

    public async Task<PublicOrderResponse> PlaceAsync(string slug, PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var establishment = await FindEstablishmentAsync(slug, cancellationToken);
        var lines = await SnapshotLinesAsync(establishment.Id, request.Items, cancellationToken);

        var order = Order.Place(establishment.Id, request.CustomerName, lines, pickupCodes.Generate(),
            PublicToken.Generate(), timeProvider.GetUtcNowForStorage());

        var payment = await paymentGateway.ChargeAsync(
            new PaymentRequest(establishment.Id, order.Id, order.TotalCents), cancellationToken);
        if (!payment.Approved)
        {
            throw new DomainException(PaymentDeclined);
        }

        order.RecordPayment(payment.TransactionId ?? string.Empty);
        db.Orders.Add(order);
        await SaveWithFreshIdentifiersOnCollisionAsync(order, cancellationToken);
        // After the commit; not tied to the request token, so a committed order is always announced.
        await notifier.OrderCreatedAsync(order, CancellationToken.None);

        return OrderMappings.ToPublicOrder(order, establishment);
    }

    public async Task<PublicOrderResponse> GetByPublicTokenAsync(string publicToken, CancellationToken cancellationToken)
    {
        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking()
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.PublicToken == publicToken, cancellationToken)
            ?? throw new NotFoundException(OrderNotFound);
        var establishment = await db.Establishments.AsNoTracking()
            .SingleAsync(e => e.Id == order.EstablishmentId, cancellationToken);

        return OrderMappings.ToPublicOrder(order, establishment);
    }

    private async Task<Establishment> FindEstablishmentAsync(string slug, CancellationToken cancellationToken) =>
        await db.Establishments.AsNoTracking().SingleOrDefaultAsync(e => e.Slug == slug, cancellationToken)
        ?? throw new NotFoundException(PublicMenuService.EstablishmentNotFound);

    /// <summary>
    /// Snapshots every requested item with its current name, effective price and station. Any sold-out, removed,
    /// foreign or unknown item rejects the whole cart; only sold-out items of this establishment reveal their name.
    /// </summary>
    private async Task<IReadOnlyList<OrderLineSnapshot>> SnapshotLinesAsync(
        Guid establishmentId, IReadOnlyList<PlaceOrderLineRequest> requested, CancellationToken cancellationToken)
    {
        var ids = requested.Select(l => l.MenuItemId).Distinct().ToList();
        var items = await db.MenuItems.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.EstablishmentId == establishmentId && ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var unavailable = ids
            .Where(id => !items.TryGetValue(id, out var item) || !item.IsAvailable)
            .Select(id => new UnavailableItemResponse
            {
                MenuItemId = id,
                Name = items.TryGetValue(id, out var soldOut) ? soldOut.Name : null,
            })
            .ToList();
        if (unavailable.Count > 0)
        {
            throw new ItemsUnavailableException(unavailable);
        }

        return requested
            .Select(line =>
            {
                var item = items[line.MenuItemId];
                return new OrderLineSnapshot(item.Id, item.Name, item.EffectivePriceCents, item.Station, line.Quantity);
            })
            .ToList();
    }

    // A pickup code is unique per establishment among active orders, and the public token is globally unique:
    // on a collision the order gets new identifiers and is saved again.
    private async Task SaveWithFreshIdentifiersOnCollisionAsync(Order order, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (UniqueConstraintViolationException exception)
                when (attempt < MaxIdentifierAttempts && exception.ConstraintName is PickupCodeIndex or PublicTokenIndex)
            {
                order.ReassignIdentifiers(pickupCodes.Generate(), PublicToken.Generate());
            }
        }
    }
}
