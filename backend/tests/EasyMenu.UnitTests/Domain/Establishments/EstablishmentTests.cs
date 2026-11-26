using EasyMenu.Domain.Common;
using EasyMenu.Domain.Establishments;
using FluentAssertions;

namespace EasyMenu.UnitTests.Domain.Establishments;

public sealed class EstablishmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_trimmed_name_slug_id_and_creation_date()
    {
        var establishment = Establishment.Create("  Bar do Zé  ", Slug.Create("bar-do-ze"), Now);

        establishment.Name.Should().Be("Bar do Zé");
        establishment.Slug.Should().Be("bar-do-ze");
        establishment.Id.Should().NotBe(Guid.Empty);
        establishment.CreatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void Name_length_limits_are_inclusive(int length)
    {
        var name = new string('x', length);

        Establishment.Create(name, Slug.Create("abc"), Now).Name.Should().Be(name);
    }

    // Decision (Testador): the contract has no dedicated code for an invalid establishment name, so the
    // domain guard uses validation_failed (the API normally rejects it earlier via request validation).
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" a ")]
    public void Name_shorter_than_two_characters_after_trim_is_rejected(string name)
    {
        var act = () => Establishment.Create(name, Slug.Create("abc"), Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }

    [Fact]
    public void Name_longer_than_one_hundred_characters_is_rejected()
    {
        var act = () => Establishment.Create(new string('x', 101), Slug.Create("abc"), Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }

    [Fact]
    public void Rename_trims_and_changes_the_name()
    {
        var establishment = Establishment.Create("Bar do Zé", Slug.Create("bar-do-ze"), Now);

        establishment.Rename("  Boteco do Zé ");

        establishment.Name.Should().Be("Boteco do Zé");
        establishment.Slug.Should().Be("bar-do-ze", "the slug is immutable");
    }

    [Theory]
    [InlineData(" a ")]
    [InlineData("")]
    public void Rename_validates_the_name_and_keeps_the_previous_one(string name)
    {
        var establishment = Establishment.Create("Bar do Zé", Slug.Create("bar-do-ze"), Now);

        var act = () => establishment.Rename(name);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
        establishment.Name.Should().Be("Bar do Zé");
    }

    [Fact]
    public void Rename_rejects_names_longer_than_one_hundred_characters()
    {
        var establishment = Establishment.Create("Bar do Zé", Slug.Create("bar-do-ze"), Now);

        var act = () => establishment.Rename(new string('x', 101));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation_failed");
    }

    [Fact]
    public void Slug_has_no_public_setter()
    {
        var setter = typeof(Establishment).GetProperty(nameof(Establishment.Slug))!.GetSetMethod(nonPublic: false);

        setter.Should().BeNull("the slug is immutable after registration (FR-003)");
    }
}
