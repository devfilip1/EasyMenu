using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Auth;

/// <summary>
/// JWT pipeline (research R1/R2): every request validates signature, lifetime (against the API's
/// <see cref="TimeProvider"/>), and — in OnTokenValidated — that the user is active and the <c>sstamp</c>
/// claim matches the current SecurityStamp. All failures give 401 on GET /api/auth/me.
/// </summary>
public sealed class TokenValidationTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const string MeRoute = "/api/auth/me";

    private Task<HttpResponseMessage> GetMeAsync(string token) => Api.CreateClient(token).GetAsync(MeRoute);

    [Fact]
    public async Task Valid_token_is_accepted()
    {
        var owner = await Api.RegisterOwnerAsync();

        (await GetMeAsync(owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_with_tampered_signature_returns_401()
    {
        var owner = await Api.RegisterOwnerAsync();
        var parts = owner.Token.Split('.');
        var signature = parts[2].ToCharArray();
        signature[0] = signature[0] == 'A' ? 'B' : 'A';
        var tampered = $"{parts[0]}.{parts[1]}.{new string(signature)}";

        (await GetMeAsync(tampered)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_with_tampered_payload_returns_401()
    {
        var victim = await Api.RegisterOwnerAsync();
        var attacker = await Api.RegisterOwnerAsync();

        // Attacker keeps its own signature but rewrites every claim that points to itself.
        var parts = attacker.Token.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]))
            .Replace(attacker.UserId.ToString(), victim.UserId.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace(attacker.EstablishmentId.ToString(), victim.EstablishmentId.ToString(), StringComparison.OrdinalIgnoreCase);
        var tampered = $"{parts[0]}.{Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}.{parts[2]}";

        (await GetMeAsync(tampered)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_signed_with_another_key_returns_401()
    {
        var owner = await Api.RegisterOwnerAsync();
        var parts = owner.Token.Split('.');
        var forged = Sign(parts[0], parts[1], "attacker-signing-key-attacker-signing-key-0123456789");

        (await GetMeAsync(forged)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unsigned_token_returns_401()
    {
        var owner = await Api.RegisterOwnerAsync();
        var payload = owner.Token.Split('.')[1];
        var header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}"""u8.ToArray());

        (await GetMeAsync($"{header}.{payload}.")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_is_still_valid_just_before_expiring()
    {
        var owner = await Api.RegisterOwnerAsync();

        Factory.Clock.Advance(TimeSpan.FromMinutes(EasyMenuApiFactory.JwtExpiresMinutes - 5));

        (await GetMeAsync(owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Expired_token_returns_401()
    {
        var owner = await Api.RegisterOwnerAsync();
        (await GetMeAsync(owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Past the 8 h lifetime plus any clock-skew tolerance.
        Factory.Clock.Advance(TimeSpan.FromMinutes(EasyMenuApiFactory.JwtExpiresMinutes) + TimeSpan.FromMinutes(10));

        (await GetMeAsync(owner.Token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_of_user_whose_security_stamp_changed_returns_401()
    {
        var owner = await Api.RegisterOwnerAsync();
        (await GetMeAsync(owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        await Api.SetSecurityStampAsync(owner.UserId, Guid.NewGuid().ToString("N"));

        (await GetMeAsync(owner.Token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_of_inactive_user_returns_401()
    {
        var owner = await Api.RegisterOwnerAsync();
        var kitchen = await Api.CreateKitchenUserAsync(owner.EstablishmentId);
        var auth = await Api.LoginAsync(kitchen.Email, kitchen.Password);
        (await GetMeAsync(auth.AccessToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Only IsActive changes: the stamp is untouched, so the active check must be independent.
        await Api.SetUserActiveAsync(kitchen.UserId, false);

        (await GetMeAsync(auth.AccessToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_of_inactive_owner_returns_401()
    {
        var owner = await Api.RegisterOwnerAsync();

        await Api.SetUserActiveAsync(owner.UserId, false);

        (await GetMeAsync(owner.Token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string Sign(string header, string payload, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes($"{header}.{payload}"));
        return $"{header}.{payload}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }
}
