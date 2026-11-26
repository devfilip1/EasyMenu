using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>SignalR client helpers bound to the in-memory TestServer (LongPolling; no real sockets).</summary>
public static class HubTestHelpers
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Builds (without starting) a hub connection to <paramref name="path"/> on the test server.</summary>
    public static HubConnection BuildHubConnection(EasyMenuApiFactory factory, string path, string? token)
    {
        var server = factory.Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                if (token is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true)
            .Build();
    }

    /// <summary>Connects to <c>/hubs/kitchen</c> with the given bearer token (throws if the server refuses it).</summary>
    public static async Task<HubConnection> ConnectKitchenHubAsync(this EasyMenuApiFactory factory, string? token)
    {
        var connection = BuildHubConnection(factory, "/hubs/kitchen", token);
        try
        {
            await connection.StartAsync().WaitAsync(DefaultTimeout);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Starts listening for the next <paramref name="eventName"/> immediately; await the returned task after
    /// triggering the action. Fails with <see cref="TimeoutException"/> after <paramref name="timeout"/> (5 s).
    /// </summary>
    public static Task<T> WaitForEventAsync<T>(HubConnection connection, string eventName, TimeSpan? timeout = null)
    {
        var received = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = connection.On<T>(eventName, payload => received.TrySetResult(payload));
        var effectiveTimeout = timeout ?? DefaultTimeout;

        return WaitAsync();

        async Task<T> WaitAsync()
        {
            try
            {
                return await received.Task.WaitAsync(effectiveTimeout);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException($"Hub event '{eventName}' was not received within {effectiveTimeout.TotalSeconds:0.#} s.");
            }
            finally
            {
                subscription.Dispose();
            }
        }
    }

    /// <summary>Records the names of every listed event received by <paramref name="connection"/>.</summary>
    public static ConcurrentQueue<string> RecordEvents(HubConnection connection, params string[] eventNames)
    {
        var received = new ConcurrentQueue<string>();
        foreach (var name in eventNames)
        {
            connection.On(name, [typeof(object)], _ =>
            {
                received.Enqueue(name);
                return Task.CompletedTask;
            });
        }

        return received;
    }
}
