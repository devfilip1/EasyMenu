using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Menu;
using EasyMenu.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Abstractions;

/// <summary>Persistence port used by the application services (implemented by AppDbContext).</summary>
public interface IAppDbContext
{
    DbSet<Establishment> Establishments { get; }

    DbSet<User> Users { get; }

    DbSet<Category> Categories { get; }

    DbSet<MenuItem> MenuItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
