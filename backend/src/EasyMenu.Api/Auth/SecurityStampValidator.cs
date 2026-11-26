using EasyMenu.Application.Abstractions;
using EasyMenu.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Api.Auth;

/// <summary>
/// Research R2: on every authenticated request the token is only accepted if its user still exists, is active
/// and its <c>sstamp</c> claim matches the current SecurityStamp (immediate access cut on deactivation).
/// </summary>
public static class SecurityStampValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var stamp = principal?.FindFirst(JwtClaimNames.SecurityStamp)?.Value;
        if (!Guid.TryParse(principal?.FindFirst(JwtClaimNames.Subject)?.Value, out var userId)
            || !Guid.TryParse(principal?.FindFirst(JwtClaimNames.EstablishmentId)?.Value, out var establishmentId)
            || stamp is null)
        {
            context.Fail("The token is missing required claims.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<IAppDbContext>();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.SecurityStamp, u.EstablishmentId })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (user is null || !user.IsActive || user.SecurityStamp != stamp || user.EstablishmentId != establishmentId)
        {
            context.Fail("The user is inactive or the token was revoked.");
        }
    }
}
