using System.Threading.RateLimiting;
using EasyMenu.Api.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace EasyMenu.Api.Realtime;

/// <summary>Marks a hub method limited by the <c>order-subscribe</c> policy (research R14).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RateLimitedHubMethodAttribute : Attribute;

/// <summary>
/// The HTTP rate limiter does not see hub method invocations, so this filter applies a fixed window per client IP
/// (<c>RateLimiting:OrderSubscribe</c>) to methods marked with <see cref="RateLimitedHubMethodAttribute"/>. A rejected
/// call fails with <c>HubException("rate_limited")</c>; the connection stays open.
/// </summary>
public sealed class HubMethodRateLimitFilter(IConfiguration configuration) : IHubFilter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(ip =>
        RateLimitPartition.GetFixedWindowLimiter(ip, _ => RateLimitingSetup.ReadWindow(configuration, "OrderSubscribe")));

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (invocationContext.HubMethod.IsDefined(typeof(RateLimitedHubMethodAttribute), inherit: true))
        {
            var httpContext = invocationContext.Context.GetHttpContext();
            var ip = httpContext is null ? "unknown" : RateLimitingSetup.ClientIp(httpContext);
            using var lease = _limiter.AttemptAcquire(ip);
            if (!lease.IsAcquired)
            {
                throw new HubException(RateLimitingSetup.RateLimitedCode);
            }
        }

        return await next(invocationContext);
    }

    public void Dispose() => _limiter.Dispose();
}
