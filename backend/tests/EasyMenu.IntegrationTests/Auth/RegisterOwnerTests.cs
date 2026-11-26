using System.Net;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Auth;

/// <summary>POST /api/auth/register-owner (FR-001..FR-003).</summary>
public sealed class RegisterOwnerTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private static RegisterOwnerRequest ValidRequest(string slug = "bar-do-ze", string email = "ze@bar.test") =>
        new(Name: "Zé", Email: email, Password: "Senha@1234", EstablishmentName: "Bar do Zé", Slug: slug);

    private Task<HttpResponseMessage> PostAsync(object request) =>
        Client.PostAsJsonAsync("/api/auth/register-owner", request, ApiDriver.Json);

    [Fact]
    public async Task Register_returns_201_with_an_auth_response()
    {
        var response = await PostAsync(ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await ApiDriver.ReadSuccessAsync<AuthResponse>(response, "register owner");
        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.ExpiresAt.Should().BeCloseTo(
            Factory.Clock.GetUtcNow().AddMinutes(EasyMenuApiFactory.JwtExpiresMinutes), TimeSpan.FromSeconds(5));
        auth.User.Id.Should().NotBe(Guid.Empty);
        auth.User.Name.Should().Be("Zé");
        auth.User.Email.Should().Be("ze@bar.test");
        auth.User.Role.Should().Be("owner");
        auth.User.Establishment.Id.Should().NotBe(Guid.Empty);
        auth.User.Establishment.Name.Should().Be("Bar do Zé");
        auth.User.Establishment.Slug.Should().Be("bar-do-ze");
    }

    [Fact]
    public async Task Returned_token_works_on_get_me()
    {
        var auth = await ApiDriver.ReadSuccessAsync<AuthResponse>(await PostAsync(ValidRequest()), "register owner");

        var me = await Api.CreateClient(auth.AccessToken).GetAsync("/api/auth/me");

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await ApiDriver.ReadSuccessAsync<CurrentUserDto>(me, "me");
        user.Should().BeEquivalentTo(auth.User);
    }

    [Fact]
    public async Task Owner_name_is_optional()
    {
        var response = await PostAsync(ValidRequest() with { Name = null });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ApiDriver.ReadSuccessAsync<AuthResponse>(response, "register owner")).User.Name.Should().BeNull();
    }

    [Fact]
    public async Task Slug_in_use_returns_409_slug_taken()
    {
        await Api.RegisterOwnerAsync(slug: "bar-do-ze");

        var response = await PostAsync(ValidRequest(slug: "bar-do-ze", email: "outro@bar.test"));

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(409, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("slug_taken");
    }

    [Fact]
    public async Task Email_in_use_returns_409_email_taken()
    {
        await Api.RegisterOwnerAsync(email: "ze@bar.test");

        var response = await PostAsync(ValidRequest(slug: "outro-bar", email: "ze@bar.test"));

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(409, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("email_taken");
    }

    [Fact]
    public async Task Email_in_use_with_different_case_returns_409_email_taken()
    {
        await Api.RegisterOwnerAsync(email: "ze@bar.test");

        var response = await PostAsync(ValidRequest(slug: "outro-bar", email: "  ZE@Bar.TEST "));

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(409, problem.RawBody);
        problem.Code.Should().Be("email_taken");
    }

    [Fact]
    public async Task Failed_registration_does_not_create_the_establishment()
    {
        await Api.RegisterOwnerAsync(email: "ze@bar.test");
        (await PostAsync(ValidRequest(slug: "novo-bar", email: "ze@bar.test"))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The slug of the failed attempt must still be free.
        var response = await PostAsync(ValidRequest(slug: "novo-bar", email: "novo@bar.test"));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("-abc")]
    [InlineData("abc-")]
    [InlineData("Bar-Do-Ze")]
    [InlineData("bár")]
    [InlineData("a_b")]
    public async Task Invalid_slug_returns_400(string slug)
    {
        var response = await PostAsync(ValidRequest(slug: slug));

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().BeOneOf("validation_failed", "invalid_slug");
        if (problem.Code == "validation_failed")
        {
            problem.Errors.Should().ContainKey("slug");
        }
    }

    [Fact]
    public async Task Slug_longer_than_fifty_characters_returns_400()
    {
        var response = await PostAsync(ValidRequest(slug: new string('a', 51)));

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().BeOneOf("validation_failed", "invalid_slug");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    public async Task Password_shorter_than_eight_characters_returns_400_validation_failed(string password)
    {
        var response = await PostAsync(ValidRequest() with { Password = password });

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("validation_failed");
        problem.Errors.Should().ContainKey("password");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("ze@")]
    public async Task Invalid_email_returns_400_validation_failed(string email)
    {
        var response = await PostAsync(ValidRequest(email: email));

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("validation_failed");
        problem.Errors.Should().ContainKey("email");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public async Task Establishment_name_shorter_than_two_characters_returns_400_validation_failed(string name)
    {
        var response = await PostAsync(ValidRequest() with { EstablishmentName = name });

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("validation_failed");
        problem.Errors.Should().ContainKey("establishmentName");
    }

    [Fact]
    public async Task Validation_failure_does_not_create_anything()
    {
        (await PostAsync(ValidRequest() with { Password = "curta" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var response = await PostAsync(ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }
}
