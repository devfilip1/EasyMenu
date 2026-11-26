using EasyMenu.Domain.Common;

namespace EasyMenu.Domain.Menu;

/// <summary>Menu item. Prices are integer cents (constitution IV).</summary>
public sealed class MenuItem : ITenantOwned
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int PhotoPathMaxLength = 300;

    /// <summary>R$ 100 mil: sanity ceiling for a single item.</summary>
    public const long MaxPriceCents = 10_000_000;

    private MenuItem(Guid id, Guid establishmentId, Guid categoryId, string name, string? description,
        long priceCents, long? promotionalPriceCents, bool isAvailable, Station station, int position)
    {
        Id = id;
        EstablishmentId = establishmentId;
        CategoryId = categoryId;
        Name = name;
        Description = description;
        PriceCents = priceCents;
        PromotionalPriceCents = promotionalPriceCents;
        IsAvailable = isAvailable;
        Station = station;
        Position = position;
    }

    public Guid Id { get; private set; }

    public Guid EstablishmentId { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Path relative to the image storage root (<c>{establishmentId}/{file}.{ext}</c>), or null.</summary>
    public string? PhotoPath { get; private set; }

    public long PriceCents { get; private set; }

    public long? PromotionalPriceCents { get; private set; }

    /// <summary><c>PromotionalPriceCents ?? PriceCents</c>.</summary>
    public long EffectivePriceCents => PromotionalPriceCents ?? PriceCents;

    public bool IsAvailable { get; private set; }

    public Station Station { get; private set; }

    public int Position { get; private set; }

    public static MenuItem Create(
        Guid establishmentId,
        Guid categoryId,
        string name,
        string? description,
        long priceCents,
        long? promotionalPriceCents,
        bool isAvailable,
        Station station,
        int position)
    {
        ValidatePrices(priceCents, promotionalPriceCents);
        return new MenuItem(Guid.CreateVersion7(), establishmentId, categoryId, ValidName(name), ValidDescription(description),
            priceCents, promotionalPriceCents, isAvailable, station, Category.ValidPosition(position));
    }

    /// <summary>Replaces every editable field, re-applying all invariants.</summary>
    public void Update(
        Guid categoryId,
        string name,
        string? description,
        long priceCents,
        long? promotionalPriceCents,
        bool isAvailable,
        Station station)
    {
        // Validate everything before changing anything.
        var validName = ValidName(name);
        var validDescription = ValidDescription(description);
        ValidatePrices(priceCents, promotionalPriceCents);

        CategoryId = categoryId;
        Name = validName;
        Description = validDescription;
        PriceCents = priceCents;
        PromotionalPriceCents = promotionalPriceCents;
        IsAvailable = isAvailable;
        Station = station;
    }

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;

    public void MoveTo(int position) => Position = Category.ValidPosition(position);

    public void SetPhoto(string photoPath) =>
        PhotoPath = !string.IsNullOrWhiteSpace(photoPath) && photoPath.Length <= PhotoPathMaxLength
            ? photoPath
            : throw new DomainException("validation_failed");

    public void RemovePhoto() => PhotoPath = null;

    private static void ValidatePrices(long priceCents, long? promotionalPriceCents)
    {
        if (priceCents is <= 0 or > MaxPriceCents)
        {
            throw new DomainException("invalid_price");
        }

        if (promotionalPriceCents is { } promotion && (promotion <= 0 || promotion >= priceCents))
        {
            throw new DomainException("invalid_promotional_price");
        }
    }

    private static string ValidName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length is >= 1 and <= NameMaxLength ? trimmed : throw new DomainException("validation_failed");
    }

    private static string? ValidDescription(string? description) =>
        description is null || description.Length <= DescriptionMaxLength
            ? description
            : throw new DomainException("validation_failed");
}
