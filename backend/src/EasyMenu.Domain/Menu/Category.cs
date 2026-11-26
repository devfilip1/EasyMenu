using EasyMenu.Domain.Common;

namespace EasyMenu.Domain.Menu;

/// <summary>Menu category of an establishment, ordered by <see cref="Position"/>.</summary>
public sealed class Category : ITenantOwned
{
    public const int NameMaxLength = 60;

    private Category(Guid id, Guid establishmentId, string name, int position)
    {
        Id = id;
        EstablishmentId = establishmentId;
        Name = name;
        Position = position;
    }

    public Guid Id { get; private set; }

    public Guid EstablishmentId { get; private set; }

    public string Name { get; private set; }

    public int Position { get; private set; }

    public static Category Create(Guid establishmentId, string name, int position) =>
        new(Guid.CreateVersion7(), establishmentId, ValidName(name), ValidPosition(position));

    public void Rename(string name) => Name = ValidName(name);

    public void MoveTo(int position) => Position = ValidPosition(position);

    private static string ValidName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length is >= 1 and <= NameMaxLength ? trimmed : throw new DomainException("validation_failed");
    }

    internal static int ValidPosition(int position) =>
        position >= 0 ? position : throw new DomainException("validation_failed");
}
