namespace EasyMenu.Domain.Orders;

/// <summary>4-character pickup code shown to the customer (research R7).</summary>
/// <remarks>Stub created by the test author (T046) so the tests compile; implementation belongs to T049.</remarks>
public static class PickupCode
{
    /// <summary>31 symbols, without 0, O, 1, I and L.</summary>
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public const int Length = 4;

    /// <summary>Random code from <see cref="Alphabet"/> (cryptographic RNG).</summary>
    public static string Generate() => throw new NotImplementedException();
}
