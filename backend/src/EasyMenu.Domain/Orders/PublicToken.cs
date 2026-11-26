namespace EasyMenu.Domain.Orders;

/// <summary>Unguessable public identifier of an order: 32 random bytes, base64url (43 characters) (research R7).</summary>
/// <remarks>Stub created by the test author (T046) so the tests compile; implementation belongs to T049.</remarks>
public static class PublicToken
{
    public const int Length = 43;

    public static string Generate() => throw new NotImplementedException();
}
