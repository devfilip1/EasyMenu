using EasyMenu.Application.Auth;
using EasyMenu.Application.Establishments;
using EasyMenu.Application.Menu;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMenu.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<EstablishmentService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<MenuItemService>();
        return services;
    }
}
