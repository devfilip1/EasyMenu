using EasyMenu.Domain.Common;
using EasyMenu.Domain.Menu;
using EasyMenu.Domain.Orders;
using FluentAssertions;

namespace EasyMenu.UnitTests.Domain.Orders;

/// <summary>Part transitions, derived order status and pickup (data-model "Transições de parte", FR-025..FR-027).</summary>
public sealed class OrderStatusTests
{
    private static readonly DateTimeOffset PlacedAt = new(2026, 11, 26, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = PlacedAt.AddMinutes(5);
    private static readonly DateTimeOffset MuchLater = PlacedAt.AddMinutes(15);
    private const string Code = "K7P4";

    private static Order NewOrder(bool withBar = true, bool withKitchen = true)
    {
        var lines = new List<OrderLineSnapshot>();
        if (withBar)
        {
            lines.Add(new OrderLineSnapshot(Guid.CreateVersion7(), "Caipirinha", 1990, Station.Bar, 1));
        }

        if (withKitchen)
        {
            lines.Add(new OrderLineSnapshot(Guid.CreateVersion7(), "Batata", 3000, Station.Kitchen, 1));
        }

        return Order.Place(Guid.CreateVersion7(), null, lines, Code, new string('t', 43), PlacedAt);
    }

    private static PartStatus PartOf(Order order, Station station) => order.Parts.Single(p => p.Station == station).Status;

    private static OrderPart Part(Order order, Station station) => order.Parts.Single(p => p.Station == station);

    // ------------------------------------------------------------------ allowed transitions

    [Theory]
    [InlineData(PartStatus.InPreparation)]
    [InlineData(PartStatus.Ready)]
    public void Received_can_move_forward(PartStatus target)
    {
        var order = NewOrder();

        order.MovePart(Station.Bar, target, Later);

        PartOf(order, Station.Bar).Should().Be(target);
        Part(order, Station.Bar).UpdatedAt.Should().Be(Later);
        PartOf(order, Station.Kitchen).Should().Be(PartStatus.Received, "only the targeted part changes");
    }

    [Fact]
    public void In_preparation_can_move_to_ready()
    {
        var order = NewOrder();
        order.MovePart(Station.Bar, PartStatus.InPreparation, Later);

        order.MovePart(Station.Bar, PartStatus.Ready, MuchLater);

        PartOf(order, Station.Bar).Should().Be(PartStatus.Ready);
        Part(order, Station.Bar).UpdatedAt.Should().Be(MuchLater);
    }

    [Theory]
    [InlineData(PartStatus.Received)]
    [InlineData(PartStatus.InPreparation)]
    [InlineData(PartStatus.Ready)]
    public void Repeating_the_current_status_is_a_no_op(PartStatus status)
    {
        var order = NewOrder();
        if (status != PartStatus.Received)
        {
            order.MovePart(Station.Bar, status, Later);
        }

        var updatedAt = Part(order, Station.Bar).UpdatedAt;
        var orderStatus = order.Status;

        var act = () => order.MovePart(Station.Bar, status, MuchLater);

        act.Should().NotThrow();
        PartOf(order, Station.Bar).Should().Be(status);
        Part(order, Station.Bar).UpdatedAt.Should().Be(updatedAt, "a no-op does not touch the part");
        order.Status.Should().Be(orderStatus);
    }

    [Theory]
    [InlineData(PartStatus.InPreparation, PartStatus.Received)]
    [InlineData(PartStatus.Ready, PartStatus.Received)]
    [InlineData(PartStatus.Ready, PartStatus.InPreparation)]
    public void Going_back_is_an_invalid_status_transition(PartStatus current, PartStatus target)
    {
        var order = NewOrder();
        order.MovePart(Station.Bar, current, Later);

        var act = () => order.MovePart(Station.Bar, target, MuchLater);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_status_transition");
        PartOf(order, Station.Bar).Should().Be(current);
    }

    [Fact]
    public void Moving_a_station_absent_from_the_order_is_not_found()
    {
        var order = NewOrder(withBar: true, withKitchen: false);

        var act = () => order.MovePart(Station.Kitchen, PartStatus.Ready, Later);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("not_found");
    }

    // ------------------------------------------------------------------ derived status (FR-026)

    [Fact]
    public void New_order_is_paid()
    {
        NewOrder().Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public void Any_part_in_preparation_makes_the_order_in_preparation()
    {
        var order = NewOrder();

        order.MovePart(Station.Kitchen, PartStatus.InPreparation, Later);

        order.Status.Should().Be(OrderStatus.InPreparation);
    }

    [Fact]
    public void Bar_ready_and_kitchen_received_is_in_preparation()
    {
        var order = NewOrder();

        order.MovePart(Station.Bar, PartStatus.Ready, Later);

        order.Status.Should().Be(OrderStatus.InPreparation);
    }

    [Fact]
    public void All_parts_ready_makes_the_order_ready()
    {
        var order = NewOrder();
        order.MovePart(Station.Bar, PartStatus.Ready, Later);

        order.MovePart(Station.Kitchen, PartStatus.Ready, MuchLater);

        order.Status.Should().Be(OrderStatus.Ready);
    }

    [Fact]
    public void Single_part_order_is_ready_when_its_part_is_ready()
    {
        var order = NewOrder(withBar: true, withKitchen: false);

        order.MovePart(Station.Bar, PartStatus.Ready, Later);

        order.Status.Should().Be(OrderStatus.Ready);
    }

    [Fact]
    public void Recalculate_status_is_stable()
    {
        var order = NewOrder();
        order.MovePart(Station.Bar, PartStatus.InPreparation, Later);

        order.RecalculateStatus();
        order.RecalculateStatus();

        order.Status.Should().Be(OrderStatus.InPreparation);
    }

    // ------------------------------------------------------------------ pickup with code (FR-027, FR-027a)

    private static Order ReadyOrder()
    {
        var order = NewOrder();
        order.MovePart(Station.Bar, PartStatus.Ready, Later);
        order.MovePart(Station.Kitchen, PartStatus.Ready, Later);
        return order;
    }

    private static Order PickedUpOrder()
    {
        var order = ReadyOrder();
        order.ConfirmPickup(Code, MuchLater);
        return order;
    }

    [Theory]
    [InlineData("K7P4")]
    [InlineData(" k7p4 ")]
    [InlineData("k7P4")]
    public void Right_code_confirms_the_pickup_of_a_ready_order(string provided)
    {
        var order = ReadyOrder();

        order.ConfirmPickup(provided, MuchLater);

        order.Status.Should().Be(OrderStatus.PickedUp);
        order.PickedUpAt.Should().Be(MuchLater);
    }

    [Theory]
    [InlineData("K7P5")]
    [InlineData("K7 P4")]
    [InlineData("K7P")]
    [InlineData("K7P44")]
    public void Wrong_code_is_invalid_pickup_code_and_changes_nothing(string provided)
    {
        var order = ReadyOrder();

        var act = () => order.ConfirmPickup(provided, MuchLater);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_pickup_code");
        order.Status.Should().Be(OrderStatus.Ready);
        order.PickedUpAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_code_is_pickup_code_required(string? provided)
    {
        var order = ReadyOrder();

        var act = () => order.ConfirmPickup(provided, MuchLater);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("pickup_code_required");
        order.Status.Should().Be(OrderStatus.Ready);
        order.PickedUpAt.Should().BeNull();
    }

    [Fact]
    public void Paid_order_is_order_not_ready_even_with_the_right_code()
    {
        var order = NewOrder();

        var act = () => order.ConfirmPickup(Code, Later);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("order_not_ready");
        order.Status.Should().Be(OrderStatus.Paid);
        order.PickedUpAt.Should().BeNull();
    }

    [Fact]
    public void Order_in_preparation_is_order_not_ready_even_with_the_right_code()
    {
        var order = NewOrder();
        order.MovePart(Station.Bar, PartStatus.Ready, Later);

        var act = () => order.ConfirmPickup(Code, MuchLater);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("order_not_ready");
        order.Status.Should().Be(OrderStatus.InPreparation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("XXXX")]
    public void Not_ready_is_checked_before_the_code(string provided)
    {
        var act = () => NewOrder().ConfirmPickup(provided, Later);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("order_not_ready");
    }

    [Theory]
    [InlineData("K7P4")]
    [InlineData("XXXX")]
    [InlineData("")]
    public void Already_picked_up_is_order_already_picked_up_and_keeps_the_first_time(string provided)
    {
        var order = PickedUpOrder();

        var act = () => order.ConfirmPickup(provided, MuchLater.AddMinutes(10));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("order_already_picked_up");
        order.Status.Should().Be(OrderStatus.PickedUp);
        order.PickedUpAt.Should().Be(MuchLater, "the first pickup time is kept");
    }

    [Theory]
    [InlineData(Station.Bar, PartStatus.Ready)]
    [InlineData(Station.Kitchen, PartStatus.Ready)]
    [InlineData(Station.Bar, PartStatus.InPreparation)]
    public void Any_part_change_after_pickup_is_an_invalid_status_transition(Station station, PartStatus target)
    {
        var order = PickedUpOrder();

        var act = () => order.MovePart(station, target, MuchLater.AddMinutes(1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_status_transition");
        order.Status.Should().Be(OrderStatus.PickedUp);
    }

    [Fact]
    public void Recalculate_status_keeps_picked_up()
    {
        var order = PickedUpOrder();

        order.RecalculateStatus();

        order.Status.Should().Be(OrderStatus.PickedUp);
    }
}
