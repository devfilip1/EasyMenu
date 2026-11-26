using System.Net;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Auth;

/// <summary>POST /api/auth/login (FR-004).</summary>
public sealed class LoginTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Owner_login_returns_200_with_role_owner()
    {
        var owner = await Api.RegisterOwnerAsync();

        var response = await Api.PostLoginAsync(owner.Email, owner.Password);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var auth = await ApiDriver.ReadSuccessAsync<AuthResponse>(response, "login");
        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.ExpiresAt.Should().BeCloseTo(
            Factory.Clock.GetUtcNow().AddMinutes(EasyMenuApiFactory.JwtExpiresMinutes), TimeSpan.FromSeconds(5));
        auth.User.Id.Should().Be(owner.UserId);
        auth.User.Role.Should().Be("owner");
        auth.User.Establishment.Id.Should().Be(owner.EstablishmentId);
    }

    [Fact]
    public async Task Login_token_works_on_get_me()
    {
        var owner = await Api.RegisterOwnerAsync();
        var auth = await Api.LoginAsync(owner.Email, owner.Password);

        var me = await Api.CreateClient(auth.AccessToken).GetAsync("/api/auth/me");

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiDriver.ReadSuccessAsync<CurrentUserDto>(me, "me")).Id.Should().Be(owner.UserId);
    }

    [Fact]
    public async Task Email_is_case_insensitive_on_login()
    {
        var owner = await Api.RegisterOwnerAsync(email: "ze@bar.test");

        var response = await Api.PostLoginAsync("  ZE@Bar.Test ", owner.Password);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Kitchen_login_returns_role_kitchen()
    {
        var owner = await Api.RegisterOwnerAsync();
        var kitchen = await Api.CreateKitchenUserAsync(owner.EstablishmentId);

        var auth = await Api.LoginAsync(kitchen.Email, kitchen.Password);

        auth.User.Id.Should().Be(kitchen.UserId);
        auth.User.Role.Should().Be("kitchen");
        auth.User.Establishment.Id.Should().Be(owner.EstablishmentId);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_give_the_same_401_invalid_credentials()
    {
        var owner = await Api.RegisterOwnerAsync();

        var wrongPassword = await ApiDriver.ReadProblemAsync(await Api.PostLoginAsync(owner.Email, "SenhaErrada!1"));
        var unknownEmail = await ApiDriver.ReadProblemAsync(await Api.PostLoginAsync("ninguem@bar.test", owner.Password));

        foreach (var problem in new[] { wrongPassword, unknownEmail })
        {
            problem.Status.Should().Be(401, problem.RawBody);
            problem.MediaType.Should().Be("application/problem+json");
            problem.Code.Should().Be("invalid_credentials");
        }

        // No user enumeration: apart from per-request fields (traceId), both responses are identical.
        Strip(wrongPassword.RawBody).Should().Be(Strip(unknownEmail.RawBody));
    }

    [Fact]
    public async Task Deactivated_account_gets_the_same_401_invalid_credentials()
    {
        var owner = await Api.RegisterOwnerAsync();
        var kitchen = await Api.CreateKitchenUserAsync(owner.EstablishmentId);
        await Api.SetUserActiveAsync(kitchen.UserId, false);

        var problem = await ApiDriver.ReadProblemAsync(await Api.PostLoginAsync(kitchen.Email, kitchen.Password));

        problem.Status.Should().Be(401, problem.RawBody);
        problem.Code.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Missing_fields_return_400_validation_failed()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { }, ApiDriver.Json);

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("validation_failed");
        problem.Errors.Keys.Should().Contain(["email", "password"]);
    }

    private static string Strip(string problemJson)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(problemJson)!.AsObject();
        node.Remove("traceId");
        node.Remove("instance");
        return node.ToJsonString();
    }
}
