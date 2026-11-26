using System.Net;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Establishment;

/// <summary>GET/PUT /api/establishment (FR-003, FR-005). Owner only.</summary>
public sealed class EstablishmentEndpointsTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Route = "/api/establishment";

    [Fact]
    public async Task Get_returns_the_owners_establishment()
    {
        var owner = await Api.RegisterOwnerAsync(slug: "bar-do-ze");

        var response = await Api.CreateClient(owner.Token).GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var establishment = await ApiDriver.ReadSuccessAsync<EstablishmentDto>(response, "get establishment");
        establishment.Id.Should().Be(owner.EstablishmentId);
        establishment.Slug.Should().Be("bar-do-ze");
        establishment.Name.Should().Be(owner.Response.User.Establishment.Name);
    }

    [Fact]
    public async Task Put_renames_with_trim_and_returns_the_establishment()
    {
        var owner = await Api.RegisterOwnerAsync(slug: "bar-do-ze");
        var client = Api.CreateClient(owner.Token);

        var response = await client.PutAsJsonAsync(Route, new UpdateEstablishmentRequest("  Boteco do Zé "), ApiDriver.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var updated = await ApiDriver.ReadSuccessAsync<EstablishmentDto>(response, "rename");
        updated.Should().Be(new EstablishmentDto(owner.EstablishmentId, "Boteco do Zé", "bar-do-ze"));
        (await ApiDriver.ReadSuccessAsync<EstablishmentDto>(await client.GetAsync(Route), "get")).Name.Should().Be("Boteco do Zé");
    }

    [Fact]
    public async Task Slug_does_not_change_even_when_sent_in_the_body()
    {
        var owner = await Api.RegisterOwnerAsync(slug: "bar-do-ze");
        var client = Api.CreateClient(owner.Token);

        var response = await client.PutAsJsonAsync(Route, new { name = "Novo Nome", slug = "outro-slug" }, ApiDriver.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await ApiDriver.ReadSuccessAsync<EstablishmentDto>(response, "rename")).Slug.Should().Be("bar-do-ze");
        (await ApiDriver.ReadSuccessAsync<EstablishmentDto>(await client.GetAsync(Route), "get")).Slug.Should().Be("bar-do-ze");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(" a ")]
    public async Task Invalid_name_returns_400(string name)
    {
        var owner = await Api.RegisterOwnerAsync();
        var client = Api.CreateClient(owner.Token);

        var problem = await ApiDriver.ReadProblemAsync(
            await client.PutAsJsonAsync(Route, new UpdateEstablishmentRequest(name), ApiDriver.Json));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("validation_failed");
        (await ApiDriver.ReadSuccessAsync<EstablishmentDto>(await client.GetAsync(Route), "get")).Name
            .Should().Be(owner.Response.User.Establishment.Name, "a rejected rename changes nothing");
    }

    [Fact]
    public async Task Name_longer_than_one_hundred_characters_returns_400()
    {
        var owner = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(owner.Token)
            .PutAsJsonAsync(Route, new UpdateEstablishmentRequest(new string('x', 101)), ApiDriver.Json));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("validation_failed");
    }

    [Fact]
    public async Task Kitchen_gets_403()
    {
        var owner = await Api.RegisterOwnerAsync();
        var kitchen = Api.CreateClient(await Api.CreateKitchenTokenAsync(owner.EstablishmentId));

        (await kitchen.GetAsync(Route)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await kitchen.PutAsJsonAsync(Route, new UpdateEstablishmentRequest("Hackeado"), ApiDriver.Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await ApiDriver.ReadSuccessAsync<EstablishmentDto>(await Api.CreateClient(owner.Token).GetAsync(Route), "get"))
            .Name.Should().Be(owner.Response.User.Establishment.Name);
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        (await Client.GetAsync(Route)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PutAsJsonAsync(Route, new UpdateEstablishmentRequest("Anônimo"), ApiDriver.Json))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Isolation_token_of_b_never_sees_or_changes_data_of_a()
    {
        var ownerA = await Api.RegisterOwnerAsync(slug: "bar-a");
        var ownerB = await Api.RegisterOwnerAsync(slug: "bar-b");
        var clientA = Api.CreateClient(ownerA.Token);
        var clientB = Api.CreateClient(ownerB.Token);

        var seenByB = await ApiDriver.ReadSuccessAsync<EstablishmentDto>(await clientB.GetAsync(Route), "get B");
        seenByB.Id.Should().Be(ownerB.EstablishmentId);
        seenByB.Slug.Should().Be("bar-b");

        (await clientB.PutAsJsonAsync(Route, new UpdateEstablishmentRequest("Renomeado por B"), ApiDriver.Json))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var a = await ApiDriver.ReadSuccessAsync<EstablishmentDto>(await clientA.GetAsync(Route), "get A");
        a.Should().Be(new EstablishmentDto(ownerA.EstablishmentId, ownerA.Response.User.Establishment.Name, "bar-a"),
            "B's rename only affects B");
    }
}
