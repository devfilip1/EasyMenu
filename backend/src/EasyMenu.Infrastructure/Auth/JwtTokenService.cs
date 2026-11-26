using EasyMenu.Application.Abstractions;
using EasyMenu.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EasyMenu.Infrastructure.Auth;

/// <summary>Issues HS256 access tokens; iat/nbf/exp come from the injected <see cref="TimeProvider"/>.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    public AccessToken Issue(User user)
    {
        var jwt = options.Value;
        // JWT times have second precision: truncate so ExpiresAt matches the token's exp claim.
        var now = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = now.AddMinutes(jwt.ExpiresMinutes);

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtClaimNames.Subject] = user.Id.ToString(),
                [JwtClaimNames.Role] = user.Role.ToString(),
                [JwtClaimNames.EstablishmentId] = user.EstablishmentId.ToString(),
                [JwtClaimNames.SecurityStamp] = user.SecurityStamp,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            },
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expiresAt);
    }
}
