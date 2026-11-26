using EasyMenu.Application.Abstractions;
using EasyMenu.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EasyMenu.Api.Auth;

public static class AuthenticationSetup
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>JWT bearer (HS256 only; issuer, audience, lifetime and signature validated), policies and tenant.</summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((options, jwtOptions, timeProvider) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TimeProvider = timeProvider;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ClockSkew = ClockSkew,
                    // Lifetime is checked against the app's TimeProvider (FakeTimeProvider in tests).
                    LifetimeValidator = (notBefore, expires, _, _) => IsWithinLifetime(notBefore, expires, timeProvider),
                    NameClaimType = JwtClaimNames.Subject,
                    RoleClaimType = JwtClaimNames.Role,
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ReadHubAccessToken,
                    OnTokenValidated = SecurityStampValidator.ValidateAsync,
                };
            });

        services.AddAuthorizationBuilder().AddEasyMenuPolicies();
        return services;
    }

    private static bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return expires is not null
            && now < expires.Value.ToUniversalTime() + ClockSkew
            && (notBefore is null || now >= notBefore.Value.ToUniversalTime() - ClockSkew);
    }

    // SignalR sends the token in the query string; accept it only for hub endpoints.
    private static Task ReadHubAccessToken(MessageReceivedContext context)
    {
        var accessToken = context.Request.Query["access_token"].ToString();
        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }
}
