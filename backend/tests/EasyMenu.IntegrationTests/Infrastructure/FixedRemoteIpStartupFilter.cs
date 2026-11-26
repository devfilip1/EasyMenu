using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only startup filter that runs before the whole API pipeline and fixes
/// <c>Connection.RemoteIpAddress</c> (TestServer leaves it null), simulating the TCP peer — e.g. the reverse proxy.
/// </summary>
public sealed class FixedRemoteIpStartupFilter(IPAddress remoteIp) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = remoteIp;
            return nextMiddleware(context);
        });
        next(app);
    };
}
