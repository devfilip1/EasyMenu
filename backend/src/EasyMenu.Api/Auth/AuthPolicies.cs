using EasyMenu.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace EasyMenu.Api.Auth;

/// <summary>Authorization policies (research R1).</summary>
public static class AuthPolicies
{
    /// <summary>Only the establishment owner.</summary>
    public const string OwnerOnly = nameof(OwnerOnly);

    /// <summary>Kitchen panel: Kitchen staff or the owner (FR-033a).</summary>
    public const string KitchenAccess = nameof(KitchenAccess);

    public static AuthorizationBuilder AddEasyMenuPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(OwnerOnly, policy => policy.RequireRole(nameof(Role.Owner)))
        .AddPolicy(KitchenAccess, policy => policy.RequireRole(nameof(Role.Owner), nameof(Role.Kitchen)));
}
