using System.Text.RegularExpressions;
using EasyMenu.Domain.Common;

namespace EasyMenu.Domain.Establishments;

/// <summary>Public, immutable establishment identifier used in the menu URL.</summary>
public sealed partial record Slug
{
    public const int MaxLength = 50;
    public const string Pattern = "^[a-z0-9](?:[a-z0-9-]{1,48})[a-z0-9]$";

    private Slug(string value) => Value = value;

    public string Value { get; }

    /// <summary>Trims and lower-cases <paramref name="value"/>; throws <c>invalid_slug</c> when it does not match the rule.</summary>
    public static Slug Create(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return SlugRegex().IsMatch(normalized)
            ? new Slug(normalized)
            : throw new DomainException("invalid_slug");
    }

    public override string ToString() => Value;

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
