namespace EasyMenu.Application.Abstractions;

/// <summary>Hashes and verifies user passwords (implemented by IdentityPasswordHasher in Infrastructure).</summary>
/// <remarks>Signature created by the test author (T006) so the test driver compiles; implementation belongs to T011/T013.</remarks>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
