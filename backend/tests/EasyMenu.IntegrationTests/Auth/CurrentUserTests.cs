using System.Net;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Auth;

/// <summary>GET /api/auth/me.</summary>
public sealed class CurrentUserTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Without_token_returns_401()
    {
        var response = await Client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Owner_gets_200_with_own_user_and_establishment()
    {
        var owner = await Api.RegisterOwnerAsync(slug: "bar-do-ze", email: "ze@bar.test");

        var response = await Api.CreateClient(owner.Token).GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await ApiDriver.ReadSuccessAsync<CurrentUserDto>(response, "me");
        me.Id.Should().Be(owner.UserId);
        me.Email.Should().Be("ze@bar.test");
        me.Role.Should().Be("owner");
        me.Establishment.Id.Should().Be(owner.EstablishmentId);
        me.Establishment.Slug.Should().Be("bar-do-ze");
        me.Establishment.Name.Should().Be(owner.Response.User.Establishment.Name);
    }

    [Fact]
    public async Task Kitchen_user_gets_role_kitchen_and_its_establishment()
    {
        var owner = await Api.RegisterOwnerAsync();
        var kitchen = await Api.CreateKitchenUserAsync(owner.EstablishmentId, name: "Ana");
        var auth = await Api.LoginAsync(kitchen.Email, kitchen.Password);

        var me = await ApiDriver.ReadSuccessAsync<CurrentUserDto>(
            await Api.CreateClient(auth.AccessToken).GetAsync("/api/auth/me"), "me");

        me.Id.Should().Be(kitchen.UserId);
        me.Name.Should().Be("Ana");
        me.Role.Should().Be("kitchen");
        me.Establishment.Id.Should().Be(owner.EstablishmentId);
    }

    [Fact]
    public async Task Each_establishment_only_sees_itself()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();

        var meA = await ApiDriver.ReadSuccessAsync<CurrentUserDto>(await Api.CreateClient(ownerA.Token).GetAsync("/api/auth/me"), "me A");
        var meB = await ApiDriver.ReadSuccessAsync<CurrentUserDto>(await Api.CreateClient(ownerB.Token).GetAsync("/api/auth/me"), "me B");

        meA.Establishment.Id.Should().Be(ownerA.EstablishmentId);
        meB.Establishment.Id.Should().Be(ownerB.EstablishmentId);
        meA.Id.Should().NotBe(meB.Id);
    }
}
