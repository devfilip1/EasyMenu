namespace EasyMenu.Domain.Common;

/// <summary>Business data owned by exactly one establishment; gets the tenant query filter (research R3).</summary>
public interface ITenantOwned
{
    Guid EstablishmentId { get; }
}
