using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace EasyMenu.Api.RateLimiting;

/// <summary>
/// Research R14/R18: behind the front's reverse proxy the client IP comes from <c>X-Forwarded-For</c>, but only
/// when the TCP peer is a trusted proxy listed in <c>ForwardedHeaders:KnownProxies</c> (IPs) or
/// <c>ForwardedHeaders:KnownNetworks</c> (CIDR). Empty lists mean "trust nobody", so the header cannot be spoofed.
/// </summary>
public static class ForwardedHeadersSetup
{
    public const string SectionName = "ForwardedHeaders";

    public static IServiceCollection AddApiForwardedHeaders(this IServiceCollection services)
    {
        // Read lazily so test hosts can override ForwardedHeaders:* settings.
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            // ASP.NET trusts loopback by default; only the configured proxies are trusted here.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
#pragma warning disable ASPDEPR005 // Legacy list, cleared so its loopback default can never apply.
            options.KnownNetworks.Clear();
#pragma warning restore ASPDEPR005

            var section = configuration.GetSection(SectionName);
            foreach (var proxy in Values(section, "KnownProxies"))
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in Values(section, "KnownNetworks"))
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }

            // The middleware trusts every peer when both lists are empty, so then nothing is forwarded at all.
            var anyTrusted = options.KnownProxies.Count > 0 || options.KnownIPNetworks.Count > 0;
            options.ForwardedHeaders = anyTrusted
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                : ForwardedHeaders.None;
            options.ForwardLimit = 1;
        });

        return services;
    }

    private static IEnumerable<string> Values(IConfiguration section, string key) =>
        (section.GetSection(key).Get<string[]>() ?? []).Where(value => !string.IsNullOrWhiteSpace(value));
}
