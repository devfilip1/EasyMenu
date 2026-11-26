namespace EasyMenu.Application.Common;

/// <summary>
/// Unique indexes that protect business rules against races, and the error code each one means (FR-002, FR-003).
/// The names follow the snake_case naming convention of the EF Core model.
/// </summary>
public static class UniqueConstraints
{
    public const string UserNormalizedEmail = "ix_users_normalized_email";
    public const string EstablishmentSlug = "ix_establishments_slug";

    /// <summary>The error code of a violated constraint, or null when it has no business meaning (a bug: 500).</summary>
    public static string? ErrorCodeFor(string constraintName) => constraintName switch
    {
        UserNormalizedEmail => ErrorCodes.EmailTaken,
        EstablishmentSlug => ErrorCodes.SlugTaken,
        _ => null,
    };
}
