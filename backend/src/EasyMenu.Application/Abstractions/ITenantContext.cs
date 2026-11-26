namespace EasyMenu.Application.Abstractions;

/// <summary>The authenticated caller, resolved from the token claims (null when anonymous).</summary>
public interface ITenantContext
{
    Guid? UserId { get; }

    Guid? EstablishmentId { get; }
}
