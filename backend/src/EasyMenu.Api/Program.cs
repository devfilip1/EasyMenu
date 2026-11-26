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

public partial class Program;
