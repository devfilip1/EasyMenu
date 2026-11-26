using EasyMenu.Api.Hubs;
using EasyMenu.Api.Serialization;
using EasyMenu.Application.Abstractions;

namespace EasyMenu.Api.Realtime;

public static class RealtimeSetup
{
    /// <summary>SignalR with the same JSON conventions as the REST API, and the order notifier.</summary>
    public static IServiceCollection AddApiRealtime(this IServiceCollection services)
    {
        services.AddSignalR().AddJsonProtocol(options => ApiJsonOptions.Apply(options.PayloadSerializerOptions));
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
