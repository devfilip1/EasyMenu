using System.Text.RegularExpressions;
using EasyMenu.Domain.Orders;
using FluentAssertions;

namespace EasyMenu.UnitTests.Domain.Orders;

/// <summary>PickupCode and PublicToken generation (research R7, FR-021, FR-031a).</summary>
public sealed partial class PickupCodeTests
{
    private const int Samples = 10_000;

    private static readonly char[] Ambiguous = ['0', 'O', '1', 'I', 'L'];

    [GeneratedRegex("^[2-9A-HJKMNP-Z]{4}$")]
    private static partial Regex PickupCodeFormat();

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex Base64UrlToken();

    [Fact]
    public void Generated_codes_always_match_the_format()
    {
        for (var i = 0; i < Samples; i++)
        {
            var code = PickupCode.Generate();
            code.Should().MatchRegex(PickupCodeFormat(), "sample {0}", i);
        }
    }

    [Fact]
    public void Generated_codes_never_contain_ambiguous_characters()
    {
        var codes = Enumerable.Range(0, Samples).Select(_ => PickupCode.Generate()).ToList();

        codes.Should().NotContain(c => c.IndexOfAny(Ambiguous) >= 0);
    }

    [Fact]
    public void Generated_codes_are_spread_over_the_alphabet()
    {
        var used = Enumerable.Range(0, Samples).SelectMany(_ => PickupCode.Generate()).ToHashSet();

        used.Should().BeEquivalentTo(PickupCode.Alphabet.ToHashSet(), "a cryptographic RNG over 40k symbols hits all 31");
    }

    [Fact]
    public void Alphabet_has_thirty_one_unambiguous_symbols()
    {
        PickupCode.Alphabet.Should().Be("23456789ABCDEFGHJKMNPQRSTUVWXYZ");
        PickupCode.Alphabet.Distinct().Should().HaveCount(31);
    }

    [Fact]
    public void Public_token_has_forty_three_base64url_characters()
    {
        for (var i = 0; i < 1_000; i++)
        {
            PublicToken.Generate().Should().MatchRegex(Base64UrlToken());
        }
    }

    [Fact]
    public void Public_tokens_are_unique()
    {
        var tokens = Enumerable.Range(0, Samples).Select(_ => PublicToken.Generate()).ToList();

        tokens.Should().OnlyHaveUniqueItems();
    }
}
