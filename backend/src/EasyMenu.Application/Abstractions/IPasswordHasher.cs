namespace EasyMenu.Application.Abstractions;

/// <summary>Hashes and verifies user passwords (implemented by IdentityPasswordHasher in Infrastructure).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
