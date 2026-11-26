using EasyMenu.Api.Realtime;
using EasyMenu.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Api.Hubs;

/// <summary>
/// Public order page hub (<c>/hubs/orders</c>, anonymous): only who knows the long public token can subscribe to
/// an order's updates (FR-031a, contracts/realtime.md).
/// </summary>
[AllowAnonymous]
public sealed class OrdersHub(IAppDbContext db) : Hub
{
    public const string Path = "/hubs/orders";

    public static string GroupOf(string publicToken) => $"order:{publicToken}";

    /// <summary>Joins the order's group when the token exists; false (and no subscription) otherwise.</summary>
    /// <remarks>Rate limited per IP (token scanning protection, research R14).</remarks>
    [RateLimitedHubMethod]
    public async Task<bool> SubscribeToOrder(string publicToken)
    {
        // No tenant here: the lookup is by the unguessable public token only.
        var exists = await db.Orders.IgnoreQueryFilters().AnyAsync(o => o.PublicToken == publicToken, Context.ConnectionAborted);
        if (exists)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(publicToken), Context.ConnectionAborted);
        }

        return exists;
    }
}
