using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Staff;

/// <summary>
/// Kitchen members of the owner's establishment (FR-032..FR-034). Users are not tenant-filtered by the DbContext,
/// so every query filters by the caller's establishment and the Kitchen role explicitly.
/// </summary>
public sealed class StaffService(
    IAppDbContext db, ITenantContext tenant, IPasswordHasher passwordHasher, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<StaffMemberResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var members = await Members().AsNoTracking().OrderBy(u => u.CreatedAt).ToListAsync(cancellationToken);
        return members.Select(ToResponse).ToList();
    }

    public async Task<StaffMemberResponse> CreateAsync(CreateStaffMemberRequest request, CancellationToken cancellationToken)
    {
        // E-mails are unique across the platform; a concurrent duplicate is caught by the unique index (→ 409).
        var normalizedEmail = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new ConflictException(ErrorCodes.EmailTaken);
        }

        var member = User.CreateKitchen(tenant.RequireEstablishmentId(), request.Name, request.Email,
            passwordHasher.Hash(request.Password), NowAtDatabasePrecision());

        db.Users.Add(member);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(member);
    }

    /// <summary>Idempotent. Rotating the SecurityStamp revokes the member's issued tokens immediately.</summary>
    public async Task<StaffMemberResponse> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        // The owner (and anyone of another establishment) is not a staff member: 404.
        var member = await Members().SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException();

        member.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(member);
    }

    /// <summary>
    /// PostgreSQL timestamptz keeps microseconds: truncating here makes the 201 body equal to what later reads return.
    /// </summary>
    private DateTimeOffset NowAtDatabasePrecision()
    {
        var now = timeProvider.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private IQueryable<User> Members()
    {
        var establishmentId = tenant.RequireEstablishmentId();
        return db.Users.Where(u => u.EstablishmentId == establishmentId && u.Role == Role.Kitchen);
    }

    private static StaffMemberResponse ToResponse(User member) => new()
    {
        Id = member.Id,
        Name = member.Name ?? string.Empty,
        Email = member.Email,
        IsActive = member.IsActive,
        CreatedAt = member.CreatedAt,
    };
}
