namespace EasyMenu.Domain.Users;

/// <remarks>Stub created by the test author (T005) so the tests compile; implementation belongs to T010.</remarks>
public sealed class User
{
    private User()
    {
    }

    public Guid Id => throw new NotImplementedException();

    public Guid EstablishmentId => throw new NotImplementedException();

    public string? Name => throw new NotImplementedException();

    public string Email => throw new NotImplementedException();

    public string NormalizedEmail => throw new NotImplementedException();

    public string PasswordHash => throw new NotImplementedException();

    public Role Role => throw new NotImplementedException();

    public bool IsActive => throw new NotImplementedException();

    public string SecurityStamp => throw new NotImplementedException();

    public DateTimeOffset CreatedAt => throw new NotImplementedException();

    public static User CreateOwner(Guid establishmentId, string? name, string email, string passwordHash, DateTimeOffset createdAt) =>
        throw new NotImplementedException();

    public static User CreateKitchen(Guid establishmentId, string name, string email, string passwordHash, DateTimeOffset createdAt) =>
        throw new NotImplementedException();

    /// <summary>Deactivates the account and rotates <see cref="SecurityStamp"/> (idempotent). Owners cannot be deactivated.</summary>
    public void Deactivate() => throw new NotImplementedException();
}
