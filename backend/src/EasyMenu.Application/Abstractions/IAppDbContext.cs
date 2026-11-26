using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Menu;
using EasyMenu.Domain.Orders;
using EasyMenu.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EasyMenu.Application.Abstractions;

/// <summary>Persistence port used by the application services (implemented by AppDbContext).</summary>
public interface IAppDbContext
{
    DbSet<Establishment> Establishments { get; }

    DbSet<User> Users { get; }

    DbSet<Category> Categories { get; }

    DbSet<MenuItem> MenuItems { get; }

    DbSet<Order> Orders { get; }

    /// <summary>Used to discard stale entities before retrying after a concurrency conflict.</summary>
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
