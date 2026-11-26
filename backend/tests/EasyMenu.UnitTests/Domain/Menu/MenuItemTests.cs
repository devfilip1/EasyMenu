using EasyMenu.Domain.Common;
using EasyMenu.Domain.Menu;
using FluentAssertions;

namespace EasyMenu.UnitTests.Domain.Menu;

public sealed class MenuItemTests
{
    private const long MaxPriceCents = 10_000_000; // R$ 100 mil (data-model)

    private static readonly Guid EstablishmentId = Guid.CreateVersion7();
    private static readonly Guid CategoryId = Guid.CreateVersion7();

    private static MenuItem NewItem(
        long priceCents = 2500,
        long? promotionalPriceCents = null,
        string name = "Caipirinha",
        string? description = "Limão e cachaça",
        bool isAvailable = true) =>
        MenuItem.Create(EstablishmentId, CategoryId, name, description, priceCents, promotionalPriceCents, isAvailable, Station.Bar, 2);

    [Fact]
    public void Create_sets_every_field()
    {
        var item = NewItem(priceCents: 2500, promotionalPriceCents: 1990);

        item.Id.Should().NotBe(Guid.Empty);
        item.EstablishmentId.Should().Be(EstablishmentId);
        item.CategoryId.Should().Be(CategoryId);
        item.Name.Should().Be("Caipirinha");
        item.Description.Should().Be("Limão e cachaça");
        item.PriceCents.Should().Be(2500);
        item.PromotionalPriceCents.Should().Be(1990);
        item.IsAvailable.Should().BeTrue();
        item.Station.Should().Be(Station.Bar);
        item.Position.Should().Be(2);
    }

    // ------------------------------------------------------------------ price

    [Theory]
    [InlineData(1)]
    [InlineData(MaxPriceCents)]
    public void Price_limits_are_inclusive(long priceCents)
    {
        NewItem(priceCents: priceCents).PriceCents.Should().Be(priceCents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(MaxPriceCents + 1)]
    public void Invalid_price_is_rejected(long priceCents)
    {
        var act = () => NewItem(priceCents: priceCents);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_price");
    }

    // ------------------------------------------------------------------ promotional price

    [Theory]
    [InlineData(2500)] // equal to the price
    [InlineData(2501)] // above the price
    [InlineData(0)]
    [InlineData(-100)]
    public void Invalid_promotional_price_is_rejected(long promotionalPriceCents)
    {
        var act = () => NewItem(priceCents: 2500, promotionalPriceCents: promotionalPriceCents);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_promotional_price");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2499)]
    public void Promotional_price_between_zero_and_the_price_is_valid(long promotionalPriceCents)
    {
        NewItem(priceCents: 2500, promotionalPriceCents: promotionalPriceCents)
            .PromotionalPriceCents.Should().Be(promotionalPriceCents);
    }

    [Fact]
    public void Null_promotional_price_is_valid()
    {
        NewItem(promotionalPriceCents: null).PromotionalPriceCents.Should().BeNull();
    }

    // ------------------------------------------------------------------ effective price

    [Fact]
    public void Effective_price_is_the_promotional_price_when_present()
    {
        NewItem(priceCents: 2500, promotionalPriceCents: 1990).EffectivePriceCents.Should().Be(1990);
    }

    [Fact]
    public void Effective_price_is_the_price_without_promotion()
    {
        NewItem(priceCents: 2500, promotionalPriceCents: null).EffectivePriceCents.Should().Be(2500);
    }

    // ------------------------------------------------------------------ name and description

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Name_length_limits_are_inclusive(int length)
    {
        var name = new string('x', length);

        NewItem(name: name).Name.Should().Be(name);
    }

    [Fact]
    public void Name_is_trimmed()
    {
        NewItem(name: "  Caipirinha  ").Name.Should().Be("Caipirinha");
    }

    // Decision (Testador): invalid name/description use validation_failed (no dedicated ErrorCode in the contract).
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_name_is_rejected(string name)
    {
        var act = () => NewItem(name: name);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }

    [Fact]
    public void Name_longer_than_one_hundred_characters_is_rejected()
    {
        var act = () => NewItem(name: new string('x', 101));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }

    [Fact]
    public void Description_is_optional()
    {
        NewItem(description: null).Description.Should().BeNull();
    }

    [Fact]
    public void Description_of_five_hundred_characters_is_accepted()
    {
        var description = new string('d', 500);

        NewItem(description: description).Description.Should().Be(description);
    }

    [Fact]
    public void Description_longer_than_five_hundred_characters_is_rejected()
    {
        var act = () => NewItem(description: new string('d', 501));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public void Update_replaces_every_editable_field()
    {
        var item = NewItem(priceCents: 2500, promotionalPriceCents: 1990);
        var newCategory = Guid.CreateVersion7();

        item.Update(newCategory, " Caipiroska ", null, 3000, null, false, Station.Kitchen);

        item.CategoryId.Should().Be(newCategory);
        item.Name.Should().Be("Caipiroska");
        item.Description.Should().BeNull();
        item.PriceCents.Should().Be(3000);
        item.PromotionalPriceCents.Should().BeNull();
        item.EffectivePriceCents.Should().Be(3000);
        item.IsAvailable.Should().BeFalse();
        item.Station.Should().Be(Station.Kitchen);
    }

    [Fact]
    public void Update_reapplies_the_promotional_price_invariant()
    {
        var item = NewItem(priceCents: 2500, promotionalPriceCents: 1990);

        // Lowering the price below the existing promotion must be rejected.
        var act = () => item.Update(CategoryId, "Caipirinha", null, 1500, 1990, true, Station.Bar);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_promotional_price");
        item.PriceCents.Should().Be(2500, "a rejected update changes nothing");
        item.PromotionalPriceCents.Should().Be(1990);
    }

    [Fact]
    public void Update_reapplies_the_price_invariant()
    {
        var item = NewItem();

        var act = () => item.Update(CategoryId, "Caipirinha", null, 0, null, true, Station.Bar);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_price");
        item.PriceCents.Should().Be(2500);
    }

    [Fact]
    public void Update_reapplies_the_name_invariant()
    {
        var item = NewItem();

        var act = () => item.Update(CategoryId, " ", null, 2500, null, true, Station.Bar);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
        item.Name.Should().Be("Caipirinha");
    }

    // ------------------------------------------------------------------ availability and position

    [Fact]
    public void SetAvailability_toggles_sold_out()
    {
        var item = NewItem(isAvailable: true);

        item.SetAvailability(false);
        item.IsAvailable.Should().BeFalse();

        item.SetAvailability(true);
        item.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void MoveTo_changes_the_position()
    {
        var item = NewItem();

        item.MoveTo(5);

        item.Position.Should().Be(5);
    }

    [Fact]
    public void Negative_position_is_rejected()
    {
        var item = NewItem();

        var act = () => item.MoveTo(-1);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }
}
