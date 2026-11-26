using EasyMenu.Application.Auth;
using EasyMenu.Application.Establishments;
using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Menu;
using EasyMenu.Application.Orders;
using EasyMenu.Application.Staff;
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
        services.AddScoped<PublicMenuService>();
        services.AddScoped<StaffService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<KitchenService>();
        services.AddSingleton<IPickupCodeGenerator, RandomPickupCodeGenerator>();
        return services;
    }
}
