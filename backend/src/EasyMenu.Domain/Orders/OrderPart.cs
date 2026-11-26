using EasyMenu.Domain.Common;
using EasyMenu.Domain.Menu;

namespace EasyMenu.Domain.Orders;

/// <summary>The part of an order prepared by one station.</summary>
public sealed class OrderPart
{
    private OrderPart(Guid id, Station station, PartStatus status, DateTimeOffset updatedAt)
    {
        Id = id;
        Station = station;
        Status = status;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }

    public Station Station { get; private set; }

    public PartStatus Status { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal static OrderPart Received(Station station, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), station, PartStatus.Received, now);

    /// <summary>
    /// received → in_preparation → ready, and received → ready (FR-025). The current status is a no-op (UpdatedAt
    /// untouched); going back gives <c>invalid_status_transition</c>.
    /// </summary>
    internal void MoveTo(PartStatus target, DateTimeOffset now)
    {
        if (target == Status)
        {
            return;
        }

        if (target < Status)
        {
            throw new DomainException(Order.InvalidStatusTransition);
        }

        Status = target;
        UpdatedAt = now;
    }
}
