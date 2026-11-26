using System.Globalization;
using EasyMenu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the API in memory against a real PostgreSQL 17 container (one per test collection), with test-only
/// JWT settings, a temporary image directory and a controllable clock (<see cref="Clock"/>).
/// </summary>
public sealed class EasyMenuApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtIssuer = "easymenu-tests";
    public const string JwtAudience = "easymenu-tests-app";
    public const string JwtSigningKey = "test-only-signing-key-easymenu-0123456789-abcdef";
    public const int JwtExpiresMinutes = 480;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("easymenu_tests")
        .WithUsername("easymenu")
        .WithPassword("easymenu")
        .Build();

    private readonly SwitchableTimeProvider _timeProvider = new();

    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>Directory used as <c>Storage:ImagesPath</c>; unique per run and deleted on dispose.</summary>
    public string ImagesPath { get; } =
        Path.Combine(Path.GetTempPath(), "easymenu-tests", Guid.NewGuid().ToString("N"), "uploads");

    /// <summary>
    /// The clock the API sees as its <see cref="TimeProvider"/> singleton. A fresh one (starting at the real
    /// current time) is installed before every test by <see cref="ResetClock"/>; advance it with
    /// <c>Factory.Clock.Advance(...)</c>.
    /// </summary>
    public FakeTimeProvider Clock => _timeProvider.Current;

    private WebApplicationFactory<Program>? _withTestControllers;

    /// <summary>
    /// A second host (same database, settings and clock) that also exposes the test-only controllers of this
    /// assembly (e.g. <see cref="ErrorTestController"/>). Kept separate so they never show up in /openapi/v1.json.
    /// </summary>
    public WebApplicationFactory<Program> WithTestControllers =>
        _withTestControllers ??= WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(EasyMenuApiFactory).Assembly)));

    /// <summary>Rate-limit policies configured by the tests (research R14): <c>RateLimiting:{Policy}:*</c>.</summary>
    public static readonly string[] RateLimitPolicies = ["Auth", "Checkout", "OrderSubscribe"];

    /// <summary>Permit limit used by the shared host, high enough that normal tests are never limited.</summary>
    public const int HighPermitLimit = 10000;

    public const int RateLimitWindowSeconds = 60;

    /// <summary>
    /// A new host (same database, settings and clock) whose three rate-limit policies allow only
    /// <paramref name="permitLimit"/> requests per window. Each call gets fresh limiter state; dispose it after use.
    /// </summary>
    public WebApplicationFactory<Program> WithRateLimits(int permitLimit) =>
        WithWebHostBuilder(builder => ConfigureRateLimits(builder, permitLimit));

    private static void ConfigureRateLimits(IWebHostBuilder builder, int permitLimit)
    {
        foreach (var policy in RateLimitPolicies)
        {
            builder.UseSetting($"RateLimiting:{policy}:PermitLimit", permitLimit.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting($"RateLimiting:{policy}:WindowSeconds", RateLimitWindowSeconds.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Installs a fresh <see cref="FakeTimeProvider"/> starting at the real current time.</summary>
    public FakeTimeProvider ResetClock() => _timeProvider.Current = new FakeTimeProvider(DateTimeOffset.UtcNow);

    async Task IAsyncLifetime.InitializeAsync()
    {
        Directory.CreateDirectory(ImagesPath);
        await _postgres.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_withTestControllers is not null)
        {
            await _withTestControllers.DisposeAsync();
        }

        await base.DisposeAsync();
        await _postgres.DisposeAsync();

        var runRoot = Directory.GetParent(ImagesPath)!.FullName;
        if (Directory.Exists(runRoot))
        {
            Directory.Delete(runRoot, recursive: true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
        builder.UseSetting("Jwt:ExpiresMinutes", JwtExpiresMinutes.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Storage:ImagesPath", ImagesPath);
        ConfigureRateLimits(builder, HighPermitLimit);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(_timeProvider);
        });
    }

    /// <summary>Truncates every application table (keeps the EF migrations history).</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """
            DO $$
            DECLARE r record;
            BEGIN
              FOR r IN SELECT tablename FROM pg_tables
                       WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
              LOOP
                EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' RESTART IDENTITY CASCADE';
              END LOOP;
            END $$;
            """);
    }
}
