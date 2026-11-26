using EasyMenu.Domain.Users;

namespace EasyMenu.Application.Abstractions;

/// <summary>Issues access tokens (JWT) for authenticated users.</summary>
public interface ITokenService
{
    AccessToken Issue(User user);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
