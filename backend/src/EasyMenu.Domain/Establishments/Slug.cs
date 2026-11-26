namespace EasyMenu.Domain.Establishments;

/// <summary>Public, immutable establishment identifier used in the menu URL.</summary>
/// <remarks>Stub created by the test author (T005) so the tests compile; implementation belongs to T010.</remarks>
public sealed record Slug
{
    private Slug()
    {
    }

    public string Value => throw new NotImplementedException();

    /// <summary>Trims and lower-cases <paramref name="value"/>; throws <c>invalid_slug</c> when it does not match the rule.</summary>
    public static Slug Create(string value) => throw new NotImplementedException();
}
