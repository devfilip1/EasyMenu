using EasyMenu.Domain.Users;
using Microsoft.AspNetCore.Identity;
using IPasswordHasher = EasyMenu.Application.Abstractions.IPasswordHasher;

namespace EasyMenu.Infrastructure.Auth;

/// <summary>PBKDF2 hashing from <c>Microsoft.Extensions.Identity.Core</c> (research R1), without full ASP.NET Identity.</summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    // The Identity hasher ignores the user instance.
    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, password) is not PasswordVerificationResult.Failed;
}
