using EasyMenu.Application.Abstractions;
using EasyMenu.Infrastructure.Auth;

namespace EasyMenu.Api.Auth;

/// <summary>Tenant of the current request, read from the validated token claims (research R3).</summary>
public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? UserId => ReadGuid(JwtClaimNames.Subject);

    public Guid? EstablishmentId => ReadGuid(JwtClaimNames.EstablishmentId);

    private Guid? ReadGuid(string claimType)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return Guid.TryParse(user.FindFirst(claimType)?.Value, out var value) ? value : null;
    }
}
