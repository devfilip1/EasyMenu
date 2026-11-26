using EasyMenu.Api.Hubs;
using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Orders;
using EasyMenu.Domain.Orders;
using Microsoft.AspNetCore.SignalR;

namespace EasyMenu.Api.Realtime;

/// <summary>Publishes order events to the kitchen hub group of the order's establishment.</summary>
public sealed class SignalROrderNotifier(IHubContext<KitchenHub> kitchenHub, ILogger<SignalROrderNotifier> logger)
    : IOrderNotifier
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderUpdated = "OrderUpdated";
    public const string OrderPickedUp = "OrderPickedUp";

    public Task OrderCreatedAsync(Order order, CancellationToken cancellationToken) =>
        SendToKitchenAsync(order, OrderCreated, KitchenService.ToKitchenOrder(order), cancellationToken);

    public Task OrderChangedAsync(Order order, OrderStatus previousStatus, CancellationToken cancellationToken) =>
        order.Status == OrderStatus.PickedUp
            ? SendToKitchenAsync(order, OrderPickedUp, new OrderPickedUpEvent(order.Id, order.PickupCode), cancellationToken)
            : SendToKitchenAsync(order, OrderUpdated, KitchenService.ToKitchenOrder(order), cancellationToken);

    // Events are hints (clients resync from REST): a delivery failure is logged, never surfaced to the request.
    private async Task SendToKitchenAsync(Order order, string eventName, object payload, CancellationToken cancellationToken)
    {
        try
        {
            await kitchenHub.Clients.Group(KitchenHub.GroupOf(order.EstablishmentId))
                .SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not send {Event} for order {OrderId}.", eventName, order.Id);
        }
    }
}

/// <summary>Payload of <c>OrderPickedUp</c>: the client removes the order from the active list.</summary>
public sealed record OrderPickedUpEvent(Guid Id, string PickupCode);
