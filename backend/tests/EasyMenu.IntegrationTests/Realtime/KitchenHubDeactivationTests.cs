using System.Net;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace EasyMenu.IntegrationTests.Realtime;

/// <summary>
/// FR-034 / research R2: deactivating a kitchen member cuts access immediately, including open SignalR
/// connections (ISessionTerminator), and reconnecting with the same token is refused.
/// </summary>
public sealed class KitchenHubDeactivationTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private readonly List<HubConnection> _connections = [];

    public override async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private async Task<(HubConnection Connection, Task Closed)> ConnectWatchingCloseAsync(string token)
    {
        var connection = await Factory.ConnectKitchenHubAsync(token);
        _connections.Add(connection);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        return (connection, closed.Task);
    }

    private async Task<(RegisteredOwner Owner, StaffMemberDto Member, string Token)> ArrangeKitchenAsync(RegisteredOwner? owner = null)
    {
        owner ??= await Api.RegisterOwnerAsync();
        var (member, password) = await Api.CreateStaffAsync(owner.Token);
        return (owner, member, (await Api.LoginAsync(member.Email, password)).AccessToken);
    }

    [Fact]
    public async Task Deactivation_closes_the_open_hub_connection_within_five_seconds()
    {
        var (owner, member, token) = await ArrangeKitchenAsync();
        var (connection, closed) = await ConnectWatchingCloseAsync(token);
        connection.State.Should().Be(HubConnectionState.Connected);

        (await Api.DeactivateStaffAsync(owner.Token, member.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var finished = await Task.WhenAny(closed, Task.Delay(HubTestHelpers.DefaultTimeout));
        finished.Should().BeSameAs(closed, "the deactivated user's connection must be aborted (Closed event within 5 s)");
        connection.State.Should().Be(HubConnectionState.Disconnected);
    }

    [Fact]
    public async Task Reconnecting_with_the_same_token_after_deactivation_fails()
    {
        var (owner, member, token) = await ArrangeKitchenAsync();
        await ConnectWatchingCloseAsync(token);

        (await Api.DeactivateStaffAsync(owner.Token, member.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var act = () => Factory.ConnectKitchenHubAsync(token);
        (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Other_connections_of_the_establishment_stay_open()
    {
        var (owner, member, token) = await ArrangeKitchenAsync();
        var (_, _, otherToken) = await ArrangeKitchenAsync(owner);
        var (_, closed) = await ConnectWatchingCloseAsync(token);
        var (other, otherClosed) = await ConnectWatchingCloseAsync(otherToken);
        var (ownerConnection, ownerClosed) = await ConnectWatchingCloseAsync(owner.Token);

        (await Api.DeactivateStaffAsync(owner.Token, member.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        await Task.WhenAny(closed, Task.Delay(HubTestHelpers.DefaultTimeout));
        await Task.Delay(TimeSpan.FromSeconds(1));

        closed.IsCompleted.Should().BeTrue("sanity: the deactivated member was disconnected");
        otherClosed.IsCompleted.Should().BeFalse("only the deactivated member is disconnected");
        ownerClosed.IsCompleted.Should().BeFalse();
        other.State.Should().Be(HubConnectionState.Connected);
        ownerConnection.State.Should().Be(HubConnectionState.Connected);
    }
}
