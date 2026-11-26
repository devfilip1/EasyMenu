using EasyMenu.Infrastructure.Persistence;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EasyMenu.IntegrationTests.Persistence;

/// <summary>
/// Constitution III, defence in depth: the database itself forbids a menu item whose category belongs to another
/// establishment (composite FK (establishment_id, category_id) → categories (establishment_id, id)).
/// Uses raw SQL so the application's tenant filter and checks are bypassed.
/// </summary>
public sealed class TenantForeignKeyTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const string ForeignKeyViolation = "23503";

    private async Task ExecuteAsync(FormattableString sql)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(sql);
    }

    private Task InsertMenuItemAsync(Guid id, Guid establishmentId, Guid categoryId) =>
        ExecuteAsync($"""
            INSERT INTO menu_items (id, establishment_id, category_id, name, price_cents, is_available, station, position)
            VALUES ({id}, {establishmentId}, {categoryId}, 'Item via SQL', 1000, TRUE, 'bar', 99)
            """);

    private async Task<(RegisteredOwner A, RegisteredOwner B, CategoryDto CategoryOfA)> ArrangeAsync()
    {
        var a = await Api.RegisterOwnerAsync();
        var b = await Api.RegisterOwnerAsync();
        var categoryOfA = await Api.CreateCategoryAsync(a.Token, "Drinks");
        return (a, b, categoryOfA);
    }

    [Fact]
    public async Task Item_of_b_pointing_to_a_category_of_a_is_rejected_by_the_database()
    {
        var (_, b, categoryOfA) = await ArrangeAsync();

        var act = () => InsertMenuItemAsync(Guid.CreateVersion7(), b.EstablishmentId, categoryOfA.Id);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(ForeignKeyViolation);
        (await Api.ListMenuItemsAsync(b.Token)).Should().BeEmpty();
    }

    [Fact]
    public async Task Item_of_a_pointing_to_its_own_category_is_accepted()
    {
        var (a, _, categoryOfA) = await ArrangeAsync();
        var id = Guid.CreateVersion7();

        await InsertMenuItemAsync(id, a.EstablishmentId, categoryOfA.Id);

        (await Api.ListMenuItemsAsync(a.Token)).Select(i => i.Id).Should().Contain(id);
    }

    [Fact]
    public async Task Moving_an_item_of_a_to_establishment_b_is_rejected_by_the_database()
    {
        var (a, b, categoryOfA) = await ArrangeAsync();
        var item = await Api.CreateMenuItemAsync(a.Token, categoryOfA.Id, "Caipirinha", 2500, station: "bar");

        var act = () => ExecuteAsync($"UPDATE menu_items SET establishment_id = {b.EstablishmentId} WHERE id = {item.Id}");

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(ForeignKeyViolation);
        (await Api.ListMenuItemsAsync(a.Token)).Select(i => i.Id).Should().Contain(item.Id);
    }
}
