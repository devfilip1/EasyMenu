using System.Reflection;
using EasyMenu.Application.Abstractions;
using EasyMenu.Domain.Common;
using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Menu;
using EasyMenu.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : DbContext(options), IAppDbContext
{
    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public DbSet<Establishment> Establishments => Set<Establishment>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Category> Categories => Set<Category>();

    /// <summary>Read by the tenant query filter on every query (null = anonymous: tenant data is hidden).</summary>
    private Guid? CurrentEstablishmentId => tenant.EstablishmentId;

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
