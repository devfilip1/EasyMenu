using System.Net;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Auth;

/// <summary>
/// 401/403 bodies: authentication and authorization failures must be RFC 7807 problems with a stable
/// <c>code</c> (<c>unauthorized</c> / <c>forbidden</c>) so the front can react to them.
/// </summary>
public sealed class AuthErrorBodyTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const string MeRoute = "/api/auth/me";

    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response)
    {
        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(401, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.BodyStatus.Should().Be(401);
        problem.Code.Should().Be("unauthorized");
        response.Headers.WwwAuthenticate.Should().NotBeEmpty("the Bearer challenge header is kept");
        response.Headers.WwwAuthenticate.First().Scheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task Missing_token_returns_401_problem_unauthorized()
    {
        await AssertUnauthorizedAsync(await Client.GetAsync(MeRoute));
    }

    [Fact]
    public async Task Tampered_token_returns_401_problem_unauthorized()
    {
        var owner = await Api.RegisterOwnerAsync();
        var parts = owner.Token.Split('.');
        var signature = parts[2].ToCharArray();
        signature[0] = signature[0] == 'A' ? 'B' : 'A';

        await AssertUnauthorizedAsync(await Api.CreateClient($"{parts[0]}.{parts[1]}.{new string(signature)}").GetAsync(MeRoute));
    }

    [Fact]
    public async Task Expired_token_returns_401_problem_unauthorized()
    {
        var owner = await Api.RegisterOwnerAsync();
        Factory.Clock.Advance(TimeSpan.FromMinutes(EasyMenuApiFactory.JwtExpiresMinutes) + TimeSpan.FromMinutes(10));

        await AssertUnauthorizedAsync(await Api.CreateClient(owner.Token).GetAsync(MeRoute));
    }

    [Fact]
    public async Task Token_of_deactivated_user_returns_401_problem_unauthorized()
    {
        var owner = await Api.RegisterOwnerAsync();
        var kitchen = await Api.CreateKitchenUserAsync(owner.EstablishmentId);
        var auth = await Api.LoginAsync(kitchen.Email, kitchen.Password);
        await Api.SetUserActiveAsync(kitchen.UserId, false);

        await AssertUnauthorizedAsync(await Api.CreateClient(auth.AccessToken).GetAsync(MeRoute));
    }

    [Fact]
    public async Task Kitchen_token_on_owner_only_endpoint_returns_403_problem_forbidden()
    {
        var owner = await Api.RegisterOwnerAsync();
        using var client = Factory.WithTestControllers.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", await Api.CreateKitchenTokenAsync(owner.EstablishmentId));

        var response = await client.GetAsync("/__tests/owner-only");

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(403, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.BodyStatus.Should().Be(403);
        problem.Code.Should().Be("forbidden");
    }

    [Fact]
    public async Task Owner_token_on_owner_only_endpoint_is_allowed()
    {
        var owner = await Api.RegisterOwnerAsync();
        using var client = Factory.WithTestControllers.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);

        (await client.GetAsync("/__tests/owner-only")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_password_on_login_stays_401_invalid_credentials()
    {
        var owner = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.PostLoginAsync(owner.Email, "SenhaErrada!1"));

        problem.Status.Should().Be(401, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("invalid_credentials", "a failed login is not a missing/invalid token");
    }
}
