using EasyMenu.Api.Hubs;
using EasyMenu.Api.Serialization;
using EasyMenu.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace EasyMenu.Api.Realtime;

public static class RealtimeSetup
{
    /// <summary>SignalR with the same JSON conventions as the REST API, and the order notifier.</summary>
    public static IServiceCollection AddApiRealtime(this IServiceCollection services)
    {
        services.AddSignalR()
            .AddJsonProtocol(options => ApiJsonOptions.Apply(options.PayloadSerializerOptions))
            .AddHubOptions<OrdersHub>(options => options.AddFilter<HubMethodRateLimitFilter>());
        // Singleton: the limiter state (per-IP windows) must outlive each invocation.
        services.AddSingleton<HubMethodRateLimitFilter>();
        services.AddScoped<IOrderNotifier, SignalROrderNotifier>();
        return services;
    }

    public static WebApplication MapApiHubs(this WebApplication app)
    {
        app.MapHub<KitchenHub>(KitchenHub.Path);
        app.MapHub<OrdersHub>(OrdersHub.Path);
        return app;
    }
}
