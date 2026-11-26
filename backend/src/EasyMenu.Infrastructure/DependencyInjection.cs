using EasyMenu.Application.Abstractions;
using EasyMenu.Infrastructure.Auth;
using EasyMenu.Infrastructure.Persistence;
using EasyMenu.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyMenu.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    /// <summary>Persistence, password hashing and tokens. The host must register <see cref="ITenantContext"/>.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<TenantSaveChangesInterceptor>();
        // Connection string is resolved lazily so test hosts can override configuration.
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(o => JwtOptions.HasValidSigningKey(o.SigningKey),
                $"Jwt:SigningKey must be set and have at least {JwtOptions.MinimumSigningKeyBytes} bytes.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience must be set.")
            .Validate(o => o.ExpiresMinutes > 0, "Jwt:ExpiresMinutes must be positive.")
            .ValidateOnStart();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.ImagesPath) && o.MaxImageBytes > 0,
                "Storage:ImagesPath must be set and Storage:MaxImageBytes must be positive.");
        services.AddSingleton<IImageStorage, LocalDiskImageStorage>();

        return services;
    }
}
