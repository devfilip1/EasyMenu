namespace EasyMenu.Infrastructure.Auth;

/// <summary>Claims of the access token (research R1).</summary>
public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string EstablishmentId = "establishment_id";
    public const string SecurityStamp = "sstamp";
}
