using EasyMenu.Domain.Common;
using EasyMenu.Domain.Menu;
using FluentAssertions;

namespace EasyMenu.UnitTests.Domain.Menu;

public sealed class CategoryTests
{
    private static readonly Guid EstablishmentId = Guid.CreateVersion7();

    [Fact]
    public void Create_sets_trimmed_name_position_and_tenant()
    {
        var category = Category.Create(EstablishmentId, "  Drinks  ", 3);

        category.Id.Should().NotBe(Guid.Empty);
        category.EstablishmentId.Should().Be(EstablishmentId);
        category.Name.Should().Be("Drinks");
        category.Position.Should().Be(3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    public void Name_length_limits_are_inclusive(int length)
    {
        var name = new string('x', length);

        Category.Create(EstablishmentId, name, 0).Name.Should().Be(name);
    }

    // Decision (Testador): no dedicated ErrorCode exists for an invalid category name, so the domain guard
    // uses validation_failed (the API rejects it earlier via request validation: minLength 1, maxLength 60).
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_name_is_rejected(string name)
    {
        var act = () => Category.Create(EstablishmentId, name, 0);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }

    [Fact]
    public void Name_longer_than_sixty_characters_is_rejected()
    {
        var act = () => Category.Create(EstablishmentId, new string('x', 61), 0);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }

    [Fact]
    public void Negative_position_is_rejected_on_create()
    {
        var act = () => Category.Create(EstablishmentId, "Drinks", -1);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }

    [Fact]
    public void Rename_trims_and_changes_the_name()
    {
        var category = Category.Create(EstablishmentId, "Drinks", 0);

        category.Rename("  Bebidas ");

        category.Name.Should().Be("Bebidas");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Rename_validates_and_keeps_the_previous_name(string name)
    {
        var category = Category.Create(EstablishmentId, "Drinks", 0);

        var act = () => category.Rename(name);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
        category.Name.Should().Be("Drinks");
    }

    [Fact]
    public void Rename_rejects_names_longer_than_sixty_characters()
    {
        var category = Category.Create(EstablishmentId, "Drinks", 0);

        var act = () => category.Rename(new string('x', 61));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
        category.Name.Should().Be("Drinks");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void MoveTo_changes_the_position(int position)
    {
        var category = Category.Create(EstablishmentId, "Drinks", 3);

        category.MoveTo(position);

        category.Position.Should().Be(position);
    }

    [Fact]
    public void MoveTo_rejects_a_negative_position()
    {
        var category = Category.Create(EstablishmentId, "Drinks", 3);

        var act = () => category.MoveTo(-1);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
        category.Position.Should().Be(3);
    }
}
