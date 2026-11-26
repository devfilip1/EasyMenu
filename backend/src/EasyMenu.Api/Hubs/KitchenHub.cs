using EasyMenu.Api.Auth;
using EasyMenu.Api.Realtime;
using EasyMenu.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EasyMenu.Api.Hubs;

/// <summary>
/// Kitchen panel hub (<c>/hubs/kitchen</c>): server → client events only. Each connection joins the group of
/// the establishment in its token, never one chosen by the client (contracts/realtime.md), and is tracked by user
/// so a deactivation can abort it.
/// </summary>
[Authorize(Policy = AuthPolicies.KitchenAccess)]
public sealed class KitchenHub(HubConnectionTracker tracker) : Hub
{
    public const string Path = "/hubs/kitchen";

    public static string GroupOf(Guid establishmentId) => $"establishment:{establishmentId}";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(ClaimId(JwtClaimNames.EstablishmentId)));
        tracker.Track(ClaimId(JwtClaimNames.Subject), Context);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        tracker.Untrack(ClaimId(JwtClaimNames.Subject), Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private Guid ClaimId(string claimType) => Guid.Parse(Context.User!.FindFirst(claimType)!.Value);
}
