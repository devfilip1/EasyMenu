using System.Collections.Concurrent;
using EasyMenu.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace EasyMenu.Api.Realtime;

/// <summary>
/// Open kitchen hub connections by user, so a deactivated user's connections can be aborted at once (research R2).
/// In memory: valid for a single API instance (scaling out needs a backplane, see plan Complexity/Risks).
/// </summary>
public sealed class HubConnectionTracker : ISessionTerminator
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, HubCallerContext>> _byUser = new();

    public void Track(Guid userId, HubCallerContext connection) =>
        _byUser.GetOrAdd(userId, _ => new ConcurrentDictionary<string, HubCallerContext>())[connection.ConnectionId] = connection;

    public void Untrack(Guid userId, string connectionId)
    {
        if (_byUser.TryGetValue(userId, out var connections))
        {
            connections.TryRemove(connectionId, out _);
        }
    }

    public void TerminateSessions(Guid userId)
    {
        if (!_byUser.TryRemove(userId, out var connections))
        {
            return;
        }

        foreach (var connection in connections.Values)
        {
            connection.Abort();
        }
    }
}
