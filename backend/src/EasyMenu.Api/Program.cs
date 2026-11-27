using System.Text.Json;
using EasyMenu.Api.Auth;
using EasyMenu.Api.ErrorHandling;
using EasyMenu.Api.Media;
using EasyMenu.Api.OpenApi;
using EasyMenu.Api.RateLimiting;
using EasyMenu.Api.Realtime;
using EasyMenu.Api.Serialization;
using EasyMenu.Application;
using EasyMenu.Infrastructure;
using EasyMenu.Infrastructure.Auth;
using EasyMenu.Infrastructure.Persistence;
using EasyMenu.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(options =>
    {
        // ModelState keys (and so ProblemDetails.errors) use the JSON names: camelCase.
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider(JsonNamingPolicy.CamelCase));
        // Enums in route and query use their snake_case names, as in JSON (e.g. ?status=picked_up).
        options.ModelBinderProviders.Insert(0, new SnakeCaseEnumModelBinderProvider());
        // A request without any body is a missing body (400 validation_failed), not an unsupported media type.
        MissingBodyAwareContentTypeFilter.Register(options);
    })
    .AddJsonOptions(options => ApiJsonOptions.Apply(options.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create);
builder.Services.ConfigureHttpJsonOptions(options => ApiJsonOptions.Apply(options.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddOpenApi(options => options
    .AddSchemaTransformer<StringEnumSchemaTransformer>()
    .AddSchemaTransformer<EmailFormatSchemaTransformer>()
    .AddOperationTransformer<RequiredFormFieldsOperationTransformer>());
builder.Services.AddApiRealtime();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
// A relative Storage:ImagesPath is relative to the content root, both for uploads and for /media.
builder.Services.PostConfigure<StorageOptions>(options =>
    options.ImagesPath = Path.GetFullPath(options.ImagesPath, builder.Environment.ContentRootPath));
builder.Services.AddApiAuthentication();
builder.Services.AddApiRateLimiting();
builder.Services.AddApiForwardedHeaders();

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

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// First: the real client IP (behind a trusted proxy) must be known before rate limiting.
app.UseForwardedHeaders();
app.UseExceptionHandler();

if (!app.Environment.IsProduction())
{
    // Generated document: /openapi/v1.json (compared against contracts/openapi.yaml in tests).
    app.MapOpenApi();
}

// Uploaded photos: /media/{establishmentId}/{file} (research R4).
var imagesPath = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value.ImagesPath;
Directory.CreateDirectory(imagesPath);
app.UseStaticFiles(MediaStaticFileOptions.Create(imagesPath));

app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapApiHubs();

app.Run();

static void EnsureRequiredConfiguration(IConfiguration configuration)
{
    var missing = new List<string>();

    if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(EasyMenu.Infrastructure.DependencyInjection.ConnectionStringName)))
    {
        missing.Add($"ConnectionStrings__{EasyMenu.Infrastructure.DependencyInjection.ConnectionStringName}");
    }

    var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (!allowedOrigins.Any(origin => !string.IsNullOrWhiteSpace(origin)))
    {
        missing.Add("Cors__AllowedOrigins (e.g. Cors__AllowedOrigins__0)");
    }

    if (!JwtOptions.HasValidSigningKey(configuration[$"{JwtOptions.SectionName}:SigningKey"]))
    {
        missing.Add($"Jwt__SigningKey (at least {JwtOptions.MinimumSigningKeyBytes} bytes)");
    }

    if (missing.Count > 0)
    {
        throw new InvalidOperationException(
            $"Missing required configuration: {string.Join(", ", missing)}. " +
            "Set them as environment variables (or in appsettings.{Environment}.json).");
    }
}

public partial class Program;
