using EasyMenu.Domain.Common;

namespace EasyMenu.Domain.Establishments;

public sealed class Establishment
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;

    private Establishment(Guid id, string name, string slug, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Slug = slug;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Immutable after registration (FR-003).</summary>
    public string Slug { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Establishment Create(string name, Slug slug, DateTimeOffset createdAt) =>
        new(Guid.CreateVersion7(), ValidName(name), slug.Value, createdAt);

    public void Rename(string name) => Name = ValidName(name);

    private static string ValidName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length is >= NameMinLength and <= NameMaxLength
            ? trimmed
            : throw new DomainException("validation_failed");
    }
}
