using System.ComponentModel.DataAnnotations;
using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Users;

namespace EasyMenu.Application.Auth;

// Request/response shapes of contracts/openapi.yaml (RegisterOwnerRequest, LoginRequest, AuthResponse,
// CurrentUser, Establishment). Validation attributes mirror the contract limits.

public sealed class RegisterOwnerRequest
{
    [StringLength(User.NameMaxLength)]
    public string? Name { get; init; }

    [Required]
    [EmailAddress]
    [StringLength(User.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [StringLength(Establishment.NameMaxLength, MinimumLength = Establishment.NameMinLength)]
    public string EstablishmentName { get; init; } = string.Empty;

    [Required]
    [StringLength(Domain.Establishments.Slug.MaxLength, MinimumLength = 3)]
    [RegularExpression(Domain.Establishments.Slug.Pattern)]
    public string Slug { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required]
    [StringLength(User.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Password { get; init; } = string.Empty;
}

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required CurrentUserResponse User { get; init; }
}

public sealed class CurrentUserResponse
{
    public required Guid Id { get; init; }

    public string? Name { get; init; }

    public required string Email { get; init; }

    public required Role Role { get; init; }

    public required EstablishmentResponse Establishment { get; init; }
}

public sealed class EstablishmentResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }
}
