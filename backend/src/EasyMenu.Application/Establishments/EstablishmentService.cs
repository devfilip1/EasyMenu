using System.ComponentModel.DataAnnotations;
using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Auth;
using EasyMenu.Domain.Establishments;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Establishments;

public sealed class UpdateEstablishmentRequest
{
    [Required]
    [StringLength(Establishment.NameMaxLength, MinimumLength = Establishment.NameMinLength)]
    public string Name { get; init; } = string.Empty;
}

/// <summary>The owner's own establishment (FR-003, FR-005). The slug is immutable.</summary>
public sealed class EstablishmentService(IAppDbContext db, ITenantContext tenant)
{
    public async Task<EstablishmentResponse> GetAsync(CancellationToken cancellationToken) =>
        ToResponse(await FindOwnAsync(cancellationToken));

    public async Task<EstablishmentResponse> RenameAsync(UpdateEstablishmentRequest request, CancellationToken cancellationToken)
    {
        var establishment = await FindOwnAsync(cancellationToken);
        establishment.Rename(request.Name);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(establishment);
    }

    // Establishment is the tenant itself (not ITenantOwned): always looked up by the caller's establishment id.
    private Task<Establishment> FindOwnAsync(CancellationToken cancellationToken)
    {
        var establishmentId = tenant.RequireEstablishmentId();
        return db.Establishments.SingleAsync(e => e.Id == establishmentId, cancellationToken);
    }

    internal static EstablishmentResponse ToResponse(Establishment establishment) => new()
    {
        Id = establishment.Id,
        Name = establishment.Name,
        Slug = establishment.Slug,
    };
}
