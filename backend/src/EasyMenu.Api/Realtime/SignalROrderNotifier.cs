using EasyMenu.Api.Hubs;
using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Orders;
using EasyMenu.Domain.Orders;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Api.Realtime;

/// <summary>
/// Publishes order events (contracts/realtime.md): to the kitchen group of the order's establishment, and, when
/// the order status changes, the <c>PublicOrder</c> to the subscribers of its public token.
/// </summary>
public sealed class SignalROrderNotifier(
    IHubContext<KitchenHub> kitchenHub,
    IHubContext<OrdersHub> ordersHub,
    IAppDbContext db,
    ILogger<SignalROrderNotifier> logger) : IOrderNotifier
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderUpdated = "OrderUpdated";
    public const string OrderPickedUp = "OrderPickedUp";

    public Task OrderCreatedAsync(Order order, CancellationToken cancellationToken) =>
        SendAsync(kitchenHub.Clients.Group(KitchenHub.GroupOf(order.EstablishmentId)), order, OrderCreated,
            KitchenService.ToKitchenOrder(order), cancellationToken);

    public async Task OrderChangedAsync(Order order, OrderStatus previousStatus, CancellationToken cancellationToken)
    {
        var kitchen = kitchenHub.Clients.Group(KitchenHub.GroupOf(order.EstablishmentId));
        await (order.Status == OrderStatus.PickedUp
            ? SendAsync(kitchen, order, OrderPickedUp, new OrderPickedUpEvent(order.Id, order.PickupCode), cancellationToken)
            : SendAsync(kitchen, order, OrderUpdated, KitchenService.ToKitchenOrder(order), cancellationToken));

        // The customer page only cares about the order status (a second part starting changes nothing for it).
        if (order.Status != previousStatus)
        {
            var establishment = await db.Establishments.AsNoTracking()
                .SingleAsync(e => e.Id == order.EstablishmentId, cancellationToken);
            await SendAsync(ordersHub.Clients.Group(OrdersHub.GroupOf(order.PublicToken)), order, OrderUpdated,
                OrderMappings.ToPublicOrder(order, establishment), cancellationToken);
        }
    }

    // Events are hints (clients resync from REST): a delivery failure is logged, never surfaced to the request.
    private async Task SendAsync(IClientProxy clients, Order order, string eventName, object payload, CancellationToken cancellationToken)
    {
        try
        {
            await clients.SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not send {Event} for order {OrderId}.", eventName, order.Id);
        }
    }
}

/// <summary>Payload of <c>OrderPickedUp</c>: the client removes the order from the active list.</summary>
public sealed record OrderPickedUpEvent(Guid Id, string PickupCode);
