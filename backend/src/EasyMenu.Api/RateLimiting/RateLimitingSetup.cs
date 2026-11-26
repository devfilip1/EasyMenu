using System.Globalization;
using System.Threading.RateLimiting;
using EasyMenu.Api.ErrorHandling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EasyMenu.Api.RateLimiting;

/// <summary>
/// Research R14: fixed-window limits per client IP, configured in <c>RateLimiting:{Auth|Checkout|OrderSubscribe}</c>.
/// A rejected request gets 429 <c>application/problem+json</c> with <c>code=rate_limited</c> and <c>Retry-After</c>.
/// </summary>
public static class RateLimitingSetup
{
    /// <summary>Login and register-owner: partitioned by IP and endpoint.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Anonymous checkout: partitioned by IP.</summary>
    public const string CheckoutPolicy = "checkout";

    /// <summary>Hub order subscriptions (applied by a hub filter, not by the HTTP middleware): partitioned by IP.</summary>
    public const string OrderSubscribePolicy = "order-subscribe";

    public const string RateLimitedCode = "rate_limited";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        // Read lazily so test hosts can override RateLimiting:* settings.
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var auth = ReadWindow(configuration, "Auth");
            var checkout = ReadWindow(configuration, "Checkout");
            var orderSubscribe = ReadWindow(configuration, "OrderSubscribe");

            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter($"{ClientIp(context)}|{EndpointKey(context)}", _ => auth));
            options.AddPolicy(CheckoutPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => checkout));
            options.AddPolicy(OrderSubscribePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => orderSubscribe));

            // Fixed-window leases carry RetryAfter; the longest window is only a fallback.
            var fallbackRetryAfter = new[] { auth.Window, checkout.Window, orderSubscribe.Window }.Max();
            options.OnRejected = (context, cancellationToken) =>
                WriteRejectionAsync(context, fallbackRetryAfter, cancellationToken);
        });

        return services;
    }

    /// <summary>Settings of one policy (<c>RateLimiting:{name}:PermitLimit|WindowSeconds</c>).</summary>
    public static FixedWindowRateLimiterOptions ReadWindow(IConfiguration configuration, string name)
    {
        var section = configuration.GetSection($"RateLimiting:{name}");
        return new FixedWindowRateLimiterOptions
        {
            PermitLimit = section.GetValue<int?>("PermitLimit")
                ?? throw new InvalidOperationException($"RateLimiting:{name}:PermitLimit is not configured."),
            Window = TimeSpan.FromSeconds(section.GetValue<int?>("WindowSeconds")
                ?? throw new InvalidOperationException($"RateLimiting:{name}:WindowSeconds is not configured.")),
            QueueLimit = 0,
            AutoReplenishment = true,
        };
    }

    /// <summary>
    /// The routed endpoint (so case or trailing-slash variations of a path share one bucket), falling back to the
    /// request path in lower case without a trailing slash.
    /// </summary>
    public static string EndpointKey(HttpContext context)
    {
        var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value ?? string.Empty;
        return pattern.Trim('/').ToLowerInvariant();
    }

    public static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext context, TimeSpan fallbackRetryAfter, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value) ? value : fallbackRetryAfter;
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
        };
        problem.Extensions[ProblemDetailsExceptionHandler.CodeExtension] = RateLimitedCode;

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }
}
