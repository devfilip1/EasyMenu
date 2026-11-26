using EasyMenu.Domain.Common;

namespace EasyMenu.Domain.Users;

public sealed class User
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int SecurityStampMaxLength = 64;

    private User(Guid establishmentId, string? name, string email, string passwordHash, Role role, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        EstablishmentId = establishmentId;
        Name = name;
        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(email);
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        SecurityStamp = NewSecurityStamp();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid EstablishmentId { get; private set; }

    public string? Name { get; private set; }

    public string Email { get; private set; }

    public string NormalizedEmail { get; private set; }

    public string PasswordHash { get; private set; }

    public Role Role { get; private set; }

    public bool IsActive { get; private set; }

    public string SecurityStamp { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static User CreateOwner(Guid establishmentId, string? name, string email, string passwordHash, DateTimeOffset createdAt) =>
        new(establishmentId, string.IsNullOrWhiteSpace(name) ? null : name.Trim(), email, passwordHash, Role.Owner, createdAt);

    public static User CreateKitchen(Guid establishmentId, string name, string email, string passwordHash, DateTimeOffset createdAt) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new DomainException("validation_failed")
            : new(establishmentId, name.Trim(), email, passwordHash, Role.Kitchen, createdAt);

    /// <summary>The unique lookup key for an e-mail: trimmed and upper-cased.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    /// <summary>Deactivates the account and rotates <see cref="SecurityStamp"/> (idempotent). Owners cannot be deactivated.</summary>
    public void Deactivate()
    {
        if (Role == Role.Owner)
        {
            throw new DomainException("cannot_deactivate_owner");
        }

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        SecurityStamp = NewSecurityStamp();
    }

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");
}
