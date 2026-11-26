using System.Reflection;
using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Common;
using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Menu;
using EasyMenu.Domain.Orders;
using EasyMenu.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EasyMenu.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : DbContext(options), IAppDbContext
{
    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public DbSet<Establishment> Establishments => Set<Establishment>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Read by the tenant query filter on every query (null = anonymous: tenant data is hidden).</summary>
    private Guid? CurrentEstablishmentId => tenant.EstablishmentId;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        MarkOrdersOfChangedPartsAsModified();
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException exception) when (UniqueViolation(exception) is { } violation)
        {
            throw violation;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        MarkOrdersOfChangedPartsAsModified();
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueViolation(exception) is { } violation)
        {
            throw violation;
        }
    }

    // The order is the consistency boundary of its parts: a part change must also update (and version-check) the
    // order row, even when the derived status did not change, so concurrent part changes always conflict on xmin.
    private void MarkOrdersOfChangedPartsAsModified()
    {
        var changedOrderIds = ChangeTracker.Entries<OrderPart>()
            .Where(e => e.State == EntityState.Modified)
            .Select(e => e.Property<Guid>("OrderId").CurrentValue)
            .ToHashSet();
        if (changedOrderIds.Count == 0)
        {
            return;
        }

        foreach (var order in ChangeTracker.Entries<Order>()
                     .Where(e => e.State == EntityState.Unchanged && changedOrderIds.Contains(e.Entity.Id)))
        {
            order.State = EntityState.Modified;
        }
    }

    // PostgreSQL 23505 (unique_violation) → UniqueConstraintViolationException with the index name.
    private static UniqueConstraintViolationException? UniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: { } name }
            ? new UniqueConstraintViolationException(name, exception)
            : null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            foreach (var property in entityType.GetProperties())
            {
                var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (enumType.IsEnum)
                {
                    property.SetValueConverter(SnakeCaseEnumConverter.For(enumType));
                }
            }

            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    // Research R3: every ITenantOwned entity is only visible to its own establishment.
    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.EstablishmentId == CurrentEstablishmentId);
}
