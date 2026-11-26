using EasyMenu.Application.Abstractions;
using EasyMenu.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EasyMenu.Infrastructure.Persistence;

/// <summary>
/// Research R3 (3): stamps new tenant-owned entities with the caller's establishment and blocks any write
/// to an entity of another establishment.
/// </summary>
public sealed class TenantSaveChangesInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    private const string EstablishmentIdProperty = nameof(ITenantOwned.EstablishmentId);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var currentTenant = tenant.EstablishmentId;
        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var establishmentId = entry.Property<Guid>(EstablishmentIdProperty);
            if (entry.State == EntityState.Added && establishmentId.CurrentValue == Guid.Empty)
            {
                establishmentId.CurrentValue = currentTenant
                    ?? throw new InvalidOperationException(
                        $"{entry.Metadata.ClrType.Name} has no EstablishmentId and there is no authenticated tenant.");
            }

            if (currentTenant is not null && establishmentId.CurrentValue != currentTenant)
            {
                throw new InvalidOperationException(
                    $"Cross-tenant write blocked: {entry.Metadata.ClrType.Name} belongs to another establishment.");
            }
        }
    }
}
