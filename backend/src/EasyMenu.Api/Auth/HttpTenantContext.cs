using EasyMenu.Application.Abstractions;
using EasyMenu.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;

namespace EasyMenu.Api.Auth;

/// <summary>
/// Tenant of the current request, read from the validated token claims (research R3). Anonymous endpoints
/// (<see cref="IAllowAnonymous"/>) never act on behalf of a tenant, even when a token is sent: a public checkout
/// by a logged-in owner of another establishment must not be treated as that owner's write.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? UserId => ReadGuid(JwtClaimNames.Subject);

    public Guid? EstablishmentId => ReadGuid(JwtClaimNames.EstablishmentId);

    private Guid? ReadGuid(string claimType)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        if (user?.Identity?.IsAuthenticated != true
            || httpContext!.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return null;
        }

        return Guid.TryParse(user.FindFirst(claimType)?.Value, out var value) ? value : null;
    }
}
