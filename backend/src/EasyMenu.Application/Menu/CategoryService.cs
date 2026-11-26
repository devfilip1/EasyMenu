using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Menu;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Menu;

/// <summary>Owner's menu categories (FR-006, FR-011). Queries are tenant-filtered by the DbContext.</summary>
public sealed class CategoryService(IAppDbContext db, ITenantContext tenant)
{
    public const string CategoryNotEmpty = "category_not_empty";

    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.Position).ToListAsync(cancellationToken);
        var itemCounts = await CountItemsAsync(cancellationToken);
        return categories.Select(c => ToResponse(c, itemCounts.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<CategoryResponse> CreateAsync(CategoryRequest request, CancellationToken cancellationToken)
    {
        var lastPosition = await db.Categories.MaxAsync(c => (int?)c.Position, cancellationToken);
        var category = Category.Create(tenant.RequireEstablishmentId(), request.Name, (lastPosition ?? -1) + 1);

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(category, itemCount: 0);
    }

    public async Task<CategoryResponse> RenameAsync(Guid id, CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await FindAsync(id, cancellationToken);
        category.Rename(request.Name);
        await db.SaveChangesAsync(cancellationToken);

        var itemCounts = await CountItemsAsync(cancellationToken);
        return ToResponse(category, itemCounts.GetValueOrDefault(category.Id));
    }

    public async Task ReorderAsync(ReorderRequest request, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.ToListAsync(cancellationToken);
        OrderList.Apply(request.Ids, categories, c => c.Id, (c, position) => c.MoveTo(position));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await FindAsync(id, cancellationToken);
        if (await db.MenuItems.AnyAsync(i => i.CategoryId == id, cancellationToken))
        {
            throw new ConflictException(CategoryNotEmpty);
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Category> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken) ?? throw new NotFoundException();

    private Task<Dictionary<Guid, int>> CountItemsAsync(CancellationToken cancellationToken) =>
        db.MenuItems
            .GroupBy(i => i.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);

    private static CategoryResponse ToResponse(Category category, int itemCount) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Position = category.Position,
        ItemCount = itemCount,
    };
}
