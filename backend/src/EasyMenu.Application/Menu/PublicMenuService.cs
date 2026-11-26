using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Menu;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Menu;

// Contract shapes PublicMenu / PublicEstablishment / PublicCategory / PublicMenuItem. Internal fields
// (station, position, categoryId, establishmentId) are never exposed (FR-038).

public sealed class PublicMenuResponse
{
    public required PublicEstablishmentResponse Establishment { get; init; }

    public required IReadOnlyList<PublicCategoryResponse> Categories { get; init; }
}

public sealed class PublicEstablishmentResponse
{
    public required string Name { get; init; }

    public required string Slug { get; init; }
}

public sealed class PublicCategoryResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<PublicMenuItemResponse> Items { get; init; }
}

public sealed class PublicMenuItemResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? PhotoUrl { get; init; }

    public required long PriceCents { get; init; }

    public long? PromotionalPriceCents { get; init; }

    public required long EffectivePriceCents { get; init; }

    public required bool IsAvailable { get; init; }
}

/// <summary>
/// Anonymous menu by slug (FR-012, FR-013). There is no tenant here, so the tenant filter is bypassed explicitly
/// and every query is restricted to the establishment resolved from the slug (research R3).
/// </summary>
public sealed class PublicMenuService(IAppDbContext db)
{
    public const string EstablishmentNotFound = "establishment_not_found";

    public async Task<PublicMenuResponse> GetAsync(string slug, CancellationToken cancellationToken)
    {
        var establishment = await db.Establishments.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Slug == slug, cancellationToken)
            ?? throw new NotFoundException(EstablishmentNotFound);

        var categories = await db.Categories.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.EstablishmentId == establishment.Id)
            .OrderBy(c => c.Position)
            .ToListAsync(cancellationToken);
        var items = await db.MenuItems.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.EstablishmentId == establishment.Id)
            .OrderBy(i => i.Position)
            .ToListAsync(cancellationToken);
        var itemsByCategory = items.ToLookup(i => i.CategoryId);

        return new PublicMenuResponse
        {
            Establishment = new PublicEstablishmentResponse { Name = establishment.Name, Slug = establishment.Slug },
            // Only categories with at least one available item; sold-out items are listed inside them.
            Categories = categories
                .Where(c => itemsByCategory[c.Id].Any(i => i.IsAvailable))
                .Select(c => new PublicCategoryResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Items = itemsByCategory[c.Id].Select(ToResponse).ToList(),
                })
                .ToList(),
        };
    }

    private static PublicMenuItemResponse ToResponse(MenuItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        PhotoUrl = MenuItemService.PhotoUrlOf(item),
        PriceCents = item.PriceCents,
        PromotionalPriceCents = item.PromotionalPriceCents,
        EffectivePriceCents = item.EffectivePriceCents,
        IsAvailable = item.IsAvailable,
    };
}
