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
}
