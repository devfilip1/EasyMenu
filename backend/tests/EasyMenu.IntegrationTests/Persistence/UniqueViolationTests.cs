using System.Net;
using System.Net.Http.Json;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Establishments;
using EstablishmentEntity = EasyMenu.Domain.Establishments.Establishment;
using EasyMenu.Domain.Users;
using EasyMenu.Infrastructure.Persistence;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMenu.IntegrationTests.Persistence;

/// <summary>
/// Unique indexes are the last line of defence against races (FR-002, FR-003): PostgreSQL 23505 is translated
/// into <see cref="UniqueConstraintViolationException"/> and, at the API, into 409 email_taken / slug_taken.
/// </summary>
public sealed class UniqueViolationTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const int Rounds = 10;

    private async Task SaveAsync(params object[] entities)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    private static EstablishmentEntity NewEstablishment(string slug) =>
        EstablishmentEntity.Create("Bar", Slug.Create(slug), DateTimeOffset.UtcNow);

    [Fact]
    public async Task Duplicate_normalized_email_throws_unique_violation_with_the_index_name()
    {
        var a = NewEstablishment("bar-a");
        var b = NewEstablishment("bar-b");
        await SaveAsync(a, b);
        await SaveAsync(User.CreateOwner(a.Id, null, "ze@bar.test", "hash", DateTimeOffset.UtcNow));

        var act = () => SaveAsync(User.CreateOwner(b.Id, null, "ZE@Bar.test", "hash", DateTimeOffset.UtcNow));

        (await act.Should().ThrowAsync<UniqueConstraintViolationException>())
            .Which.ConstraintName.Should().Be("ix_users_normalized_email");
    }

    [Fact]
    public async Task Duplicate_slug_throws_unique_violation_with_the_index_name()
    {
        await SaveAsync(NewEstablishment("bar-do-ze"));

        var act = () => SaveAsync(NewEstablishment("bar-do-ze"));

        (await act.Should().ThrowAsync<UniqueConstraintViolationException>())
            .Which.ConstraintName.Should().Be("ix_establishments_slug");
    }

    private async Task<HttpResponseMessage[]> RegisterConcurrentlyAsync(RegisterOwnerRequest first, RegisterOwnerRequest second)
    {
        using var clientA = Factory.CreateClient();
        using var clientB = Factory.CreateClient();
        return await Task.WhenAll(
            clientA.PostAsJsonAsync("/api/auth/register-owner", first, ApiDriver.Json),
            clientB.PostAsJsonAsync("/api/auth/register-owner", second, ApiDriver.Json));
    }

    private static RegisterOwnerRequest Request(string email, string slug) =>
        new(Name: null, Email: email, Password: "Senha@1234", EstablishmentName: "Bar", Slug: slug);

    private static async Task AssertOneCreatedAndOneConflictAsync(HttpResponseMessage[] responses, string code, int round)
    {
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        var summary = $"round {round}: {string.Join(" | ", responses.Select((r, i) => $"{(int)r.StatusCode} {bodies[i]}"))}";

        responses.Select(r => r.StatusCode).Should().NotContain(HttpStatusCode.InternalServerError, summary);
        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1, summary);
        var conflict = responses.Single(r => r.StatusCode != HttpStatusCode.Created);
        var problem = await ApiDriver.ReadProblemAsync(conflict);
        problem.Status.Should().Be(409, summary);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be(code, summary);
    }

    [Fact]
    public async Task Concurrent_registrations_with_the_same_email_give_one_201_and_one_409_email_taken()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var email = $"race-{round}-{Guid.NewGuid():N}@bar.test";

            var responses = await RegisterConcurrentlyAsync(
                Request(email, $"race-a-{round}-{Guid.NewGuid():N}"[..20]),
                Request(email, $"race-b-{round}-{Guid.NewGuid():N}"[..20]));

            await AssertOneCreatedAndOneConflictAsync(responses, "email_taken", round);
        }
    }

    [Fact]
    public async Task Concurrent_registrations_with_the_same_slug_give_one_201_and_one_409_slug_taken()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var slug = $"race-{round}-{Guid.NewGuid():N}"[..20];

            var responses = await RegisterConcurrentlyAsync(
                Request($"a-{round}-{Guid.NewGuid():N}@bar.test", slug),
                Request($"b-{round}-{Guid.NewGuid():N}@bar.test", slug));

            await AssertOneCreatedAndOneConflictAsync(responses, "slug_taken", round);
        }
    }
}
