using System.Buffers.Text;
using System.Security.Cryptography;

namespace EasyMenu.Domain.Orders;

/// <summary>Unguessable public identifier of an order: 32 random bytes, base64url (43 characters) (research R7).</summary>
public static class PublicToken
{
    public const int Length = 43;

    private const int Bytes = 32;

    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(Bytes));
}
