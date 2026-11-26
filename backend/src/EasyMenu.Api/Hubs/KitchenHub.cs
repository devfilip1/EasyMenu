using EasyMenu.Api.Auth;
using EasyMenu.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EasyMenu.Api.Hubs;

/// <summary>
/// Kitchen panel hub (<c>/hubs/kitchen</c>): server → client events only. Each connection joins the group of
/// the establishment in its token, never one chosen by the client (contracts/realtime.md).
/// </summary>
[Authorize(Policy = AuthPolicies.KitchenAccess)]
public sealed class KitchenHub : Hub
{
    public const string Path = "/hubs/kitchen";

    public static string GroupOf(Guid establishmentId) => $"establishment:{establishmentId}";

    public override async Task OnConnectedAsync()
    {
        var establishmentId = Guid.Parse(Context.User!.FindFirst(JwtClaimNames.EstablishmentId)!.Value);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(establishmentId));
        await base.OnConnectedAsync();
    }
}
