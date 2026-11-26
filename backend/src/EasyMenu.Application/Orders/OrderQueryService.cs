using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Orders;

/// <summary>The owner's order history (FR-035). Queries are tenant-filtered by the DbContext.</summary>
public sealed class OrderQueryService(IAppDbContext db)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Newest first; a page past the end has no items but keeps the real total count.</summary>
    public async Task<OrderSummaryPageResponse> ListAsync(
        OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking();
        if (status is { } filter)
        {
            query = query.Where(o => o.Status == filter);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummaryResponse
            {
                Id = o.Id,
                PickupCode = o.PickupCode,
                CustomerName = o.CustomerName,
                Status = o.Status,
                TotalCents = o.TotalCents,
                CreatedAt = o.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new OrderSummaryPageResponse { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<OrderDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Parts)
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException();

        return new OrderDetailResponse
        {
            Id = order.Id,
            PickupCode = order.PickupCode,
            CustomerName = order.CustomerName,
            Status = order.Status,
            TotalCents = order.TotalCents,
            CreatedAt = order.CreatedAt,
            PickedUpAt = order.PickedUpAt,
            Items = order.Items.Select(item => new OrderDetailLineResponse
            {
                Name = item.NameSnapshot,
                UnitPriceCents = item.UnitPriceCents,
                Quantity = item.Quantity,
                SubtotalCents = item.SubtotalCents,
                Station = item.Station,
            }).ToList(),
            Parts = order.Parts.OrderBy(p => p.Station)
                .Select(part => new OrderPartStatusResponse { Station = part.Station, Status = part.Status })
                .ToList(),
        };
    }
}
