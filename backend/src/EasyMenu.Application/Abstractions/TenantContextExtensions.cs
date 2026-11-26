namespace EasyMenu.Application.Abstractions;

public static class TenantContextExtensions
{
    /// <summary>
    /// The caller's establishment. Only used by endpoints that require authentication, so a missing tenant is a
    /// programming error, not a client error.
    /// </summary>
    public static Guid RequireEstablishmentId(this ITenantContext tenant) =>
        tenant.EstablishmentId ?? throw new InvalidOperationException("No authenticated establishment for this request.");
}
