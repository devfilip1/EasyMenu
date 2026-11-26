using System.ComponentModel.DataAnnotations;
using EasyMenu.Domain.Users;

namespace EasyMenu.Application.Staff;

// Request/response shapes of contracts/openapi.yaml (CreateStaffMemberRequest, StaffMember).

public sealed class CreateStaffMemberRequest
{
    [Required]
    [StringLength(User.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(User.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}

public sealed class StaffMemberResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Email { get; init; }

    public required bool IsActive { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
