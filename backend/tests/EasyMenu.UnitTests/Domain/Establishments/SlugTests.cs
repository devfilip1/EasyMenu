using EasyMenu.Domain.Common;
using EasyMenu.Domain.Establishments;
using FluentAssertions;

namespace EasyMenu.UnitTests.Domain.Establishments;

public sealed class SlugTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("bar-do-ze")]
    [InlineData("a1b")]
    [InlineData("123")]
    public void Valid_slugs_are_accepted(string value)
    {
        Slug.Create(value).Value.Should().Be(value);
    }

    [Fact]
    public void Fifty_characters_is_the_maximum_length()
    {
        var value = new string('a', 50);

        Slug.Create(value).Value.Should().Be(value);
    }

    // Decision (Testador): the data-model says Slug.Create trims and lower-cases, so "Bar" becomes "bar"
    // (valid) instead of being rejected. Upper-case input is rejected earlier by the request validation
    // (contract pattern ^[a-z0-9]...$).
    [Theory]
    [InlineData("  bar-do-ze  ", "bar-do-ze")]
    [InlineData("Bar", "bar")]
    [InlineData(" Bar-Do-Ze ", "bar-do-ze")]
    public void Slug_is_trimmed_and_lower_cased(string value, string expected)
    {
        Slug.Create(value).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("-abc")]
    [InlineData("abc-")]
    [InlineData("bár")]
    [InlineData("a_b")]
    [InlineData("bar do ze")]
    [InlineData("bar.do.ze")]
    public void Invalid_slugs_are_rejected_with_invalid_slug(string value)
    {
        var act = () => Slug.Create(value);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_slug");
    }

    [Fact]
    public void Fifty_one_characters_is_rejected_with_invalid_slug()
    {
        var act = () => Slug.Create(new string('a', 51));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_slug");
    }
}
