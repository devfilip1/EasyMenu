using EasyMenu.Domain.Common;
using EasyMenu.Domain.Menu;
using EasyMenu.Domain.Orders;
using FluentAssertions;

namespace EasyMenu.UnitTests.Domain.Orders;

/// <summary>Order.Place (data-model "Order"; FR-016..FR-019; constitution IV).</summary>
public sealed class OrderPlaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 20, 30, 0, TimeSpan.Zero);
    private static readonly Guid EstablishmentId = Guid.CreateVersion7();
    private static readonly Guid CaipirinhaId = Guid.CreateVersion7();
    private static readonly Guid ChoppId = Guid.CreateVersion7();
    private static readonly Guid BatataId = Guid.CreateVersion7();

    private const string Code = "AB2C";
    private const string Token = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ";

    private static OrderLineSnapshot Caipirinha(int quantity) => new(CaipirinhaId, "Caipirinha", 1990, Station.Bar, quantity);

    private static OrderLineSnapshot Chopp(int quantity) => new(ChoppId, "Chopp", 1200, Station.Bar, quantity);

    private static OrderLineSnapshot Batata(int quantity) => new(BatataId, "Batata", 3000, Station.Kitchen, quantity);

    private static Order Place(params OrderLineSnapshot[] lines) => Place("Zé", lines);

    private static Order Place(string? customerName, params OrderLineSnapshot[] lines) =>
        Order.Place(EstablishmentId, customerName, lines, Code, Token, Now);

    [Fact]
    public void Place_creates_a_paid_order_with_identifiers_and_timestamp()
    {
        var order = Place(Caipirinha(2), Batata(1));

        order.Id.Should().NotBe(Guid.Empty);
        order.EstablishmentId.Should().Be(EstablishmentId);
        order.PickupCode.Should().Be(Code);
        order.PublicToken.Should().Be(Token);
        order.CustomerName.Should().Be("Zé");
        order.Status.Should().Be(OrderStatus.Paid);
        order.CreatedAt.Should().Be(Now);
        order.PickedUpAt.Should().BeNull();
    }

    [Fact]
    public void Each_line_snapshots_name_effective_price_and_station()
    {
        var order = Place(Caipirinha(2), Batata(1));

        order.Items.Should().HaveCount(2);
        var caipirinha = order.Items.Single(i => i.MenuItemId == CaipirinhaId);
        caipirinha.NameSnapshot.Should().Be("Caipirinha");
        caipirinha.UnitPriceCents.Should().Be(1990);
        caipirinha.Quantity.Should().Be(2);
        caipirinha.Station.Should().Be(Station.Bar);
        var batata = order.Items.Single(i => i.MenuItemId == BatataId);
        batata.NameSnapshot.Should().Be("Batata");
        batata.UnitPriceCents.Should().Be(3000);
        batata.Station.Should().Be(Station.Kitchen);
    }

    [Fact]
    public void Subtotals_and_total_are_computed_by_the_domain()
    {
        var order = Place(Caipirinha(2), Batata(1));

        order.Items.Single(i => i.MenuItemId == CaipirinhaId).SubtotalCents.Should().Be(3980);
        order.Items.Single(i => i.MenuItemId == BatataId).SubtotalCents.Should().Be(3000);
        order.TotalCents.Should().Be(6980); // 2×1990 + 1×3000
    }

    [Fact]
    public void Repeated_lines_of_the_same_item_are_grouped()
    {
        var order = Place(Caipirinha(1), Batata(1), Caipirinha(2));

        order.Items.Should().HaveCount(2);
        var caipirinha = order.Items.Single(i => i.MenuItemId == CaipirinhaId);
        caipirinha.Quantity.Should().Be(3);
        caipirinha.SubtotalCents.Should().Be(5970);
        order.TotalCents.Should().Be(8970);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99)]
    public void Quantity_limits_are_inclusive(int quantity)
    {
        Place(Caipirinha(quantity)).Items.Single().Quantity.Should().Be(quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Quantity_outside_one_to_ninety_nine_is_rejected(int quantity)
    {
        var act = () => Place(Caipirinha(quantity));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_quantity");
    }

    [Fact]
    public void Grouped_quantity_above_ninety_nine_is_rejected()
    {
        var act = () => Place(Caipirinha(50), Caipirinha(50));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_quantity");
    }

    [Fact]
    public void Invalid_line_is_rejected_even_when_grouped_sum_would_be_valid()
    {
        var act = () => Place(Caipirinha(3), Caipirinha(0));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_quantity");
    }

    [Fact]
    public void Empty_cart_is_rejected()
    {
        var act = () => Place();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("empty_cart");
    }

    [Fact]
    public void One_received_part_per_station_present()
    {
        var order = Place(Caipirinha(1), Chopp(2), Batata(1));

        order.Parts.Should().HaveCount(2);
        order.Parts.Select(p => p.Station).Should().BeEquivalentTo([Station.Bar, Station.Kitchen]);
        order.Parts.Should().OnlyContain(p => p.Status == PartStatus.Received && p.UpdatedAt == Now);
    }

    [Fact]
    public void Bar_only_order_has_a_single_part()
    {
        var order = Place(Caipirinha(1), Chopp(1));

        order.Parts.Should().ContainSingle().Which.Station.Should().Be(Station.Bar);
    }

    [Fact]
    public void Kitchen_only_order_has_a_single_part()
    {
        Place(Batata(2)).Parts.Should().ContainSingle().Which.Station.Should().Be(Station.Kitchen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_customer_name_becomes_null(string? customerName)
    {
        Place(customerName, Caipirinha(1)).CustomerName.Should().BeNull();
    }

    [Fact]
    public void Customer_name_is_trimmed()
    {
        Place("  Maria  ", Caipirinha(1)).CustomerName.Should().Be("Maria");
    }

    [Fact]
    public void Customer_name_of_sixty_characters_is_accepted()
    {
        var name = new string('m', 60);

        Place(name, Caipirinha(1)).CustomerName.Should().Be(name);
    }

    // Decision (Testador): no dedicated ErrorCode; contract maxLength 60 → validation_failed.
    [Fact]
    public void Customer_name_longer_than_sixty_characters_is_rejected()
    {
        var act = () => Place(new string('m', 61), Caipirinha(1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }
}
