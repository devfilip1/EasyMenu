using EasyMenu.Domain.Orders;

namespace EasyMenu.Application.Abstractions;

/// <summary>
/// Real-time notifications (contracts/realtime.md), always sent after the change was committed. They are hints:
/// clients reload the state from REST, so a failed notification never fails the request.
/// </summary>
public interface IOrderNotifier
{
    /// <summary>A paid order was created.</summary>
    Task OrderCreatedAsync(Order order, CancellationToken cancellationToken);

    /// <summary>A part and/or the order status changed (<paramref name="previousStatus"/> is the order status before it).</summary>
    Task OrderChangedAsync(Order order, OrderStatus previousStatus, CancellationToken cancellationToken);
}
