using EasyMenu.Application.Auth;
using EasyMenu.Application.Establishments;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMenu.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<EstablishmentService>();
        return services;
    }
}
