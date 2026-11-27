using System.Security.Cryptography;

namespace EasyMenu.Domain.Orders;

/// <summary>4-character pickup code shown to the customer (research R7).</summary>
public static class PickupCode
{
    /// <summary>31 symbols, without 0, O, 1, I and L.</summary>
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public const int Length = 4;

    /// <summary>Random code from <see cref="Alphabet"/> (cryptographic RNG).</summary>
    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);

    /// <summary>
    /// Whether the code typed by the staff is <paramref name="expected"/>: surrounding spaces and case are ignored,
    /// inner spaces are not (<c>" k7p4 "</c> matches, <c>"K7 P4"</c> does not).
    /// </summary>
    public static bool Matches(string expected, string? provided) =>
        provided is not null && string.Equals(expected, provided.Trim().ToUpperInvariant(), StringComparison.Ordinal);
}
