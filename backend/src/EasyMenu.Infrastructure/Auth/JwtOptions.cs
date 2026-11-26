using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EasyMenu.Infrastructure.Auth;

/// <summary>JWT settings (section <c>Jwt</c>). The signing key is HS256 and must have at least 32 bytes.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int ExpiresMinutes { get; set; } = 480;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    public static bool HasValidSigningKey(string? signingKey) =>
        signingKey is not null && Encoding.UTF8.GetByteCount(signingKey) >= MinimumSigningKeyBytes;
}
