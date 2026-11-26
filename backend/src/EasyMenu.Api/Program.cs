using EasyMenu.Infrastructure;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddInfrastructure();

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()); // required by SignalR
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // Outside Development there are no defaults: fail fast instead of booting half-configured.
    EnsureRequiredConfiguration(app.Configuration);
}

if (!app.Environment.IsProduction())
{
    // Generated document: /openapi/v1.json (compared against contracts/openapi.yaml in tests).
    app.MapOpenApi();
}

app.UseCors(FrontendCorsPolicy);
app.UseAuthorization();

app.MapControllers();
// SignalR hubs are mapped here as features add them, e.g. app.MapHub<OrdersHub>("/hubs/orders").

app.Run();

static void EnsureRequiredConfiguration(IConfiguration configuration)
{
    var missing = new List<string>();

    if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(DependencyInjection.ConnectionStringName)))
    {
        missing.Add($"ConnectionStrings__{DependencyInjection.ConnectionStringName}");
    }

    var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (!allowedOrigins.Any(origin => !string.IsNullOrWhiteSpace(origin)))
    {
        missing.Add("Cors__AllowedOrigins (e.g. Cors__AllowedOrigins__0)");
    }

    if (missing.Count > 0)
    {
        throw new InvalidOperationException(
            $"Missing required configuration: {string.Join(", ", missing)}. " +
            "Set them as environment variables (or in appsettings.{Environment}.json).");
    }
}

public partial class Program;
