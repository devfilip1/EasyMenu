using System.ComponentModel.DataAnnotations;
using EasyMenu.Domain.Menu;

namespace EasyMenu.Application.Menu;

// Request/response shapes of contracts/openapi.yaml (Category, CategoryRequest, ReorderRequest).

public sealed class CategoryRequest
{
    [Required]
    [StringLength(Category.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;
}

public sealed class ReorderRequest
{
    /// <summary>Every id, in the new order.</summary>
    [Required]
    public IReadOnlyList<Guid> Ids { get; init; } = [];
}

public sealed class CategoryResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required int Position { get; init; }

    public required int ItemCount { get; init; }
}
