using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EasyMenu.Application.Abstractions;
using EasyMenu.Domain.Users;
using EasyMenu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>
/// Scenario helpers for integration tests. Each test task extends it with the helpers it needs.
/// Request/response DTOs here mirror contracts/openapi.yaml (enums as snake_case strings).
/// </summary>
public sealed class ApiDriver(EasyMenuApiFactory factory) : IDisposable
{
    public const string DefaultPassword = "Senha@1234";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _anonymous = factory.CreateClient();
    private readonly List<HttpClient> _clients = [];
    private int _sequence;

    // ------------------------------------------------------------------ auth

    /// <summary>Registers an owner + establishment through <c>POST /api/auth/register-owner</c>.</summary>
    public async Task<RegisteredOwner> RegisterOwnerAsync(string? slug = null, string? email = null)
    {
        var n = Interlocked.Increment(ref _sequence);
        var suffix = $"{n}-{Guid.NewGuid():N}"[..12];
        var request = new RegisterOwnerRequest(
            Name: $"Dono {n}",
            Email: email ?? $"owner-{suffix}@easymenu.test",
            Password: DefaultPassword,
            EstablishmentName: $"Bar {n}",
            Slug: slug ?? $"bar-{suffix}");

        var response = await _anonymous.PostAsJsonAsync("/api/auth/register-owner", request, Json);
        var auth = await ReadSuccessAsync<AuthResponse>(response, "register owner");

        return new RegisteredOwner(auth.AccessToken, auth.User.Id, auth.User.Establishment.Id, request.Email, request.Password, auth);
    }

    /// <summary>Calls <c>POST /api/auth/login</c> and returns the raw response.</summary>
    public Task<HttpResponseMessage> PostLoginAsync(string email, string password) =>
        _anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);

    /// <summary>Logs in and returns the parsed <see cref="AuthResponse"/> (fails the test if not 200).</summary>
    public async Task<AuthResponse> LoginAsync(string email, string password) =>
        await ReadSuccessAsync<AuthResponse>(await PostLoginAsync(email, password), "login");

    /// <summary>HttpClient that sends <c>Authorization: Bearer {token}</c> (disposed with the driver).</summary>
    public HttpClient CreateClient(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        _clients.Add(client);
        return client;
    }

    /// <summary>
    /// Inserts an active Kitchen user directly in the database (hash via the API's <see cref="IPasswordHasher"/>),
    /// so stories do not depend on the staff endpoints (US5).
    /// </summary>
    public async Task<KitchenCredentials> CreateKitchenUserAsync(Guid establishmentId, string? name = null)
    {
        var n = Interlocked.Increment(ref _sequence);
        var email = $"kitchen-{n}-{Guid.NewGuid():N}"[..20] + "@easymenu.test";

        await using var scope = factory.Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = User.CreateKitchen(establishmentId, name ?? $"Cozinha {n}", email, hasher.Hash(DefaultPassword), clock.GetUtcNow());
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();

        return new KitchenCredentials(user.Id, email, DefaultPassword);
    }

    /// <summary>Overwrites the user's <c>SecurityStamp</c> directly in the database.</summary>
    public Task SetSecurityStampAsync(Guid userId, string securityStamp) =>
        WithDbAsync(db => db.Set<User>()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, securityStamp)));

    /// <summary>Sets <c>IsActive</c> directly in the database (bypasses domain rules, e.g. for the owner).</summary>
    public Task SetUserActiveAsync(Guid userId, bool isActive) =>
        WithDbAsync(db => db.Set<User>()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, isActive)));

    // ------------------------------------------------------------------ errors

    /// <summary>Reads an RFC 7807 response: HTTP status, media type, <c>code</c> and <c>errors</c>.</summary>
    public static async Task<ProblemResult> ReadProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        string? code = null;
        int? bodyStatus = null;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(body))
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    code = c.GetString();
                }

                if (root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.Number)
                {
                    bodyStatus = s.GetInt32();
                }

                if (root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in e.EnumerateObject())
                    {
                        errors[field.Name] = field.Value.ValueKind == JsonValueKind.Array
                            ? field.Value.EnumerateArray().Select(m => m.GetString() ?? string.Empty).ToArray()
                            : [field.Value.ToString()];
                    }
                }
            }
        }

        return new ProblemResult((int)response.StatusCode, code, errors, mediaType, bodyStatus, body);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Asserts a 2xx response and deserializes its body, with the body in the failure message.</summary>
    public static async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response, string what)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"{what} failed: {(int)response.StatusCode} {response.StatusCode} — {body}");
        }

        return await response.Content.ReadFromJsonAsync<T>(Json)
            ?? throw new InvalidOperationException($"{what} returned an empty body.");
    }

    /// <summary>Runs <paramref name="action"/> with a scoped <see cref="AppDbContext"/> (direct DB setup/inspection).</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public void Dispose()
    {
        _anonymous.Dispose();
        foreach (var client in _clients)
        {
            client.Dispose();
        }
    }
}

public sealed record RegisterOwnerRequest(string? Name, string Email, string Password, string EstablishmentName, string Slug);

public sealed record LoginRequest(string Email, string Password);

public sealed record EstablishmentDto(Guid Id, string Name, string Slug);

public sealed record CurrentUserDto(Guid Id, string? Name, string Email, string Role, EstablishmentDto Establishment);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

public sealed record RegisteredOwner(
    string Token, Guid UserId, Guid EstablishmentId, string Email, string Password, AuthResponse Response);

public sealed record KitchenCredentials(Guid UserId, string Email, string Password);

public sealed record ProblemResult(
    int Status,
    string? Code,
    IReadOnlyDictionary<string, string[]> Errors,
    string? MediaType,
    int? BodyStatus,
    string RawBody);
