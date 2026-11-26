using System.Net;
using System.Net.Http.Json;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Staff;

/// <summary>/api/staff (FR-032..FR-034, US5). Owner only; tenant isolated.</summary>
public sealed class StaffEndpointsTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const int ConcurrentRounds = 10;

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task Create_returns_201_and_the_member_can_log_in_as_kitchen()
    {
        var owner = await Api.RegisterOwnerAsync();

        var response = await Api.PostStaffAsync(owner.Token, new CreateStaffMemberRequest(" Ana ", "ana@bar.test", "Senha@1234"));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var member = await ApiDriver.ReadSuccessAsync<StaffMemberDto>(response, "create staff");
        member.Id.Should().NotBe(Guid.Empty);
        member.Name.Should().Be("Ana");
        member.Email.Should().Be("ana@bar.test");
        member.IsActive.Should().BeTrue();
        member.CreatedAt.Should().BeCloseTo(Factory.Clock.GetUtcNow(), TimeSpan.FromSeconds(5));

        var login = await Api.LoginAsync("ana@bar.test", "Senha@1234");
        login.User.Id.Should().Be(member.Id);
        login.User.Role.Should().Be("kitchen");
        login.User.Establishment.Id.Should().Be(owner.EstablishmentId);
    }

    [Fact]
    public async Task Email_of_the_owner_returns_409_email_taken()
    {
        var owner = await Api.RegisterOwnerAsync(email: "dono@bar.test");

        var problem = await ApiDriver.ReadProblemAsync(await Api.PostStaffAsync(owner.Token,
            new CreateStaffMemberRequest("Ana", "DONO@Bar.test", "Senha@1234")));

        problem.Status.Should().Be(409, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("email_taken");
    }

    [Fact]
    public async Task Email_used_in_another_establishment_returns_409_email_taken()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        await Api.CreateStaffAsync(ownerA.Token, email: "ana@bar.test");

        var problem = await ApiDriver.ReadProblemAsync(await Api.PostStaffAsync(ownerB.Token,
            new CreateStaffMemberRequest("Ana", "  Ana@BAR.test ", "Senha@1234")));

        problem.Status.Should().Be(409, problem.RawBody);
        problem.Code.Should().Be("email_taken");
        (await Api.ListStaffAsync(ownerB.Token)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "ana@bar.test", "Senha@1234", "name")]
    [InlineData("Ana", "nao-e-email", "Senha@1234", "email")]
    [InlineData("Ana", "ana@bar.test", "curta", "password")]
    public async Task Invalid_request_returns_400_validation_failed(string name, string email, string password, string field)
    {
        var owner = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.PostStaffAsync(owner.Token, new CreateStaffMemberRequest(name, email, password)));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("validation_failed");
        problem.Errors.Should().ContainKey(field);
    }

    [Fact]
    public async Task Concurrent_creations_with_the_same_email_give_one_201_and_one_409_never_500()
    {
        var owner = await Api.RegisterOwnerAsync();

        for (var round = 0; round < ConcurrentRounds; round++)
        {
            var email = $"race-{round}-{Guid.NewGuid():N}@bar.test";
            using var clientA = Factory.CreateClient();
            using var clientB = Factory.CreateClient();
            clientA.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);
            clientB.DefaultRequestHeaders.Authorization = new("Bearer", owner.Token);

            var responses = await Task.WhenAll(
                clientA.PostAsJsonAsync("/api/staff", new CreateStaffMemberRequest("A", email, "Senha@1234"), ApiDriver.Json),
                clientB.PostAsJsonAsync("/api/staff", new CreateStaffMemberRequest("B", email, "Senha@1234"), ApiDriver.Json));

            var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
            var summary = $"round {round}: {string.Join(" | ", responses.Select((r, i) => $"{(int)r.StatusCode} {bodies[i]}"))}";
            responses.Select(r => r.StatusCode).Should().NotContain(HttpStatusCode.InternalServerError, summary);
            responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1, summary);
            var conflict = await ApiDriver.ReadProblemAsync(responses.Single(r => r.StatusCode != HttpStatusCode.Created));
            conflict.Status.Should().Be(409, summary);
            conflict.Code.Should().Be("email_taken", summary);
        }
    }

    // ------------------------------------------------------------------ list

    [Fact]
    public async Task List_contains_only_kitchen_members_of_the_establishment_including_inactive_ones()
    {
        var owner = await Api.RegisterOwnerAsync();
        var (ana, _) = await Api.CreateStaffAsync(owner.Token, "Ana");
        var (bia, _) = await Api.CreateStaffAsync(owner.Token, "Bia");
        (await Api.DeactivateStaffAsync(owner.Token, bia.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await Api.ListStaffAsync(owner.Token);

        list.Select(m => m.Id).Should().BeEquivalentTo([ana.Id, bia.Id]);
        list.Should().NotContain(m => m.Id == owner.UserId, "the owner is not part of the staff list");
        list.Single(m => m.Id == bia.Id).IsActive.Should().BeFalse();
        list.Single(m => m.Id == ana.Id).IsActive.Should().BeTrue();
    }

    // ------------------------------------------------------------------ deactivate

    [Fact]
    public async Task Deactivate_returns_200_and_is_idempotent()
    {
        var owner = await Api.RegisterOwnerAsync();
        var (ana, _) = await Api.CreateStaffAsync(owner.Token, "Ana");

        var first = await Api.DeactivateStaffAsync(owner.Token, ana.Id);
        var second = await Api.DeactivateStaffAsync(owner.Token, ana.Id);

        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        (await ApiDriver.ReadSuccessAsync<StaffMemberDto>(first, "deactivate")).Should().Be(ana with { IsActive = false });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiDriver.ReadSuccessAsync<StaffMemberDto>(second, "deactivate again")).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task After_deactivation_the_issued_token_gets_401_and_login_gets_invalid_credentials()
    {
        var owner = await Api.RegisterOwnerAsync();
        var (ana, password) = await Api.CreateStaffAsync(owner.Token, "Ana");
        var token = (await Api.LoginAsync(ana.Email, password)).AccessToken;
        (await Api.CreateClient(token).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Api.DeactivateStaffAsync(owner.Token, ana.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Api.CreateClient(token).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Api.CreateClient(token).GetAsync("/api/kitchen/orders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var login = await ApiDriver.ReadProblemAsync(await Api.PostLoginAsync(ana.Email, password));
        login.Status.Should().Be(401, login.RawBody);
        login.Code.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Deactivating_the_owner_id_returns_404()
    {
        var owner = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.DeactivateStaffAsync(owner.Token, owner.UserId));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
        (await Api.CreateClient(owner.Token).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK, "the owner stays active");
    }

    [Fact]
    public async Task Deactivating_an_unknown_id_returns_404()
    {
        var owner = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.DeactivateStaffAsync(owner.Token, Guid.NewGuid()));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("not_found");
    }

    // ------------------------------------------------------------------ roles (US5-2)

    public static TheoryData<string, string> OwnerOnlyEndpoints => new()
    {
        { "GET", "/api/staff" },
        { "POST", "/api/staff" },
        { "PUT", "/api/staff/{staff}/deactivate" },
        { "GET", "/api/categories" },
        { "POST", "/api/categories" },
        { "GET", "/api/menu-items" },
        { "POST", "/api/menu-items" },
        { "GET", "/api/orders" },
        { "GET", "/api/establishment" },
        { "PUT", "/api/establishment" },
    };

    [Theory]
    [MemberData(nameof(OwnerOnlyEndpoints))]
    public async Task Kitchen_token_gets_403_on_owner_endpoints(string method, string route)
    {
        var owner = await Api.RegisterOwnerAsync();
        var (ana, password) = await Api.CreateStaffAsync(owner.Token, "Ana");
        var kitchen = Api.CreateClient((await Api.LoginAsync(ana.Email, password)).AccessToken);
        using var request = new HttpRequestMessage(new HttpMethod(method), route.Replace("{staff}", ana.Id.ToString()));
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { name = "Intruso", email = "x@bar.test", password = "Senha@1234" }, options: ApiDriver.Json);
        }

        var response = await kitchen.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await Api.ListStaffAsync(owner.Token)).Should().ContainSingle().Which.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        var owner = await Api.RegisterOwnerAsync();
        var (ana, _) = await Api.CreateStaffAsync(owner.Token, "Ana");

        (await Client.GetAsync("/api/staff")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Api.PostStaffAsync(null, new CreateStaffMemberRequest("X", "x@bar.test", "Senha@1234")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Api.DeactivateStaffAsync(null, ana.Id)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------ isolation

    [Fact]
    public async Task Isolation_b_deactivating_the_kitchen_of_a_returns_404()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        var (anaA, password) = await Api.CreateStaffAsync(ownerA.Token, "Ana");

        var problem = await ApiDriver.ReadProblemAsync(await Api.DeactivateStaffAsync(ownerB.Token, anaA.Id));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
        (await Api.ListStaffAsync(ownerA.Token)).Single().IsActive.Should().BeTrue();
        (await Api.PostLoginAsync(anaA.Email, password)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Isolation_list_of_b_does_not_include_staff_of_a()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        await Api.CreateStaffAsync(ownerA.Token, "Ana");
        var (biaB, _) = await Api.CreateStaffAsync(ownerB.Token, "Bia");

        (await Api.ListStaffAsync(ownerB.Token)).Should().ContainSingle().Which.Id.Should().Be(biaB.Id);
    }

    [Fact]
    public async Task Isolation_b_deactivating_the_owner_of_a_returns_404()
    {
        var ownerA = await Api.RegisterOwnerAsync();
        var ownerB = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.DeactivateStaffAsync(ownerB.Token, ownerA.UserId));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
        (await Api.CreateClient(ownerA.Token).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
