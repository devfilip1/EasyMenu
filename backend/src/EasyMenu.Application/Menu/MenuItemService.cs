using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Menu;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Menu;

/// <summary>Owner's menu items (FR-007..FR-011). Queries are tenant-filtered by the DbContext.</summary>
public sealed class MenuItemService(IAppDbContext db, ITenantContext tenant)
{
    public const string CategoryNotFound = "category_not_found";
    public const string MediaPathPrefix = "/media/";

    /// <summary>Ordered by category position, then item position; includes sold-out items.</summary>
    public async Task<IReadOnlyList<MenuItemResponse>> ListAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        var query =
            from item in db.MenuItems.AsNoTracking()
            join category in db.Categories on item.CategoryId equals category.Id
            where categoryId == null || item.CategoryId == categoryId
            orderby category.Position, item.Position
            select item;

        return (await query.ToListAsync(cancellationToken)).Select(ToResponse).ToList();
    }

    public async Task<MenuItemResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await FindAsync(id, cancellationToken));

    public async Task<MenuItemResponse> CreateAsync(MenuItemRequest request, CancellationToken cancellationToken)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        var item = MenuItem.Create(
            tenant.RequireEstablishmentId(),
            request.CategoryId,
            request.Name,
            request.Description,
            request.PriceCents,
            request.PromotionalPriceCents,
            request.IsAvailable,
            request.Station,
            await NextPositionAsync(request.CategoryId, cancellationToken));

        db.MenuItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(item);
    }

    /// <summary>Replaces every field; a new category moves the item to its end.</summary>
    public async Task<MenuItemResponse> UpdateAsync(Guid id, MenuItemRequest request, CancellationToken cancellationToken)
    {
        var item = await FindAsync(id, cancellationToken);
        var categoryChanged = item.CategoryId != request.CategoryId;
        int? newPosition = null;
        if (categoryChanged)
        {
            await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
            newPosition = await NextPositionAsync(request.CategoryId, cancellationToken);
        }

        item.Update(request.CategoryId, request.Name, request.Description, request.PriceCents,
            request.PromotionalPriceCents, request.IsAvailable, request.Station);
        if (newPosition is { } position)
        {
            item.MoveTo(position);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(item);
    }

    public async Task<MenuItemResponse> SetAvailabilityAsync(Guid id, AvailabilityRequest request, CancellationToken cancellationToken)
    {
        var item = await FindAsync(id, cancellationToken);
        item.SetAvailability(request.IsAvailable);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(item);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await FindAsync(id, cancellationToken);
        db.MenuItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Orders the items of a category by a complete list of their ids.</summary>
    public async Task ReorderAsync(Guid categoryId, ReorderRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new NotFoundException();
        }

        var items = await db.MenuItems.Where(i => i.CategoryId == categoryId).ToListAsync(cancellationToken);
        OrderList.Apply(request.Ids, items, i => i.Id, (i, position) => i.MoveTo(position));
        await db.SaveChangesAsync(cancellationToken);
    }

    public static MenuItemResponse ToResponse(MenuItem item) => new()
    {
        Id = item.Id,
        CategoryId = item.CategoryId,
        Name = item.Name,
        Description = item.Description,
        PhotoUrl = item.PhotoPath is null ? null : MediaPathPrefix + item.PhotoPath,
        PriceCents = item.PriceCents,
        PromotionalPriceCents = item.PromotionalPriceCents,
        EffectivePriceCents = item.EffectivePriceCents,
        IsAvailable = item.IsAvailable,
        Station = item.Station,
        Position = item.Position,
    };

    private async Task<MenuItem> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.MenuItems.SingleOrDefaultAsync(i => i.Id == id, cancellationToken) ?? throw new NotFoundException();

    // A category of another establishment is invisible (tenant filter), so it is reported as not found.
    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new NotFoundException(CategoryNotFound);
        }
    }

    private async Task<int> NextPositionAsync(Guid categoryId, CancellationToken cancellationToken) =>
        (await db.MenuItems.Where(i => i.CategoryId == categoryId).MaxAsync(i => (int?)i.Position, cancellationToken) ?? -1) + 1;
}
