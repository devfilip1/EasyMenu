namespace EasyMenu.Domain.Menu;

/// <summary>Menu item. Prices are integer cents (constitution IV).</summary>
/// <remarks>Stub created by the test author (T024) so the tests compile; implementation belongs to T026.</remarks>
public sealed class MenuItem
{
    private MenuItem()
    {
    }

    public Guid Id => throw new NotImplementedException();

    public Guid EstablishmentId => throw new NotImplementedException();

    public Guid CategoryId => throw new NotImplementedException();

    public string Name => throw new NotImplementedException();

    public string? Description => throw new NotImplementedException();

    public long PriceCents => throw new NotImplementedException();

    public long? PromotionalPriceCents => throw new NotImplementedException();

    /// <summary><c>PromotionalPriceCents ?? PriceCents</c>.</summary>
    public long EffectivePriceCents => throw new NotImplementedException();

    public bool IsAvailable => throw new NotImplementedException();

    public Station Station => throw new NotImplementedException();

    public int Position => throw new NotImplementedException();

    public static MenuItem Create(
        Guid establishmentId,
        Guid categoryId,
        string name,
        string? description,
        long priceCents,
        long? promotionalPriceCents,
        bool isAvailable,
        Station station,
        int position) => throw new NotImplementedException();

    /// <summary>Replaces every editable field, re-applying all invariants.</summary>
    public void Update(
        Guid categoryId,
        string name,
        string? description,
        long priceCents,
        long? promotionalPriceCents,
        bool isAvailable,
        Station station) => throw new NotImplementedException();

    public void SetAvailability(bool isAvailable) => throw new NotImplementedException();

    public void MoveTo(int position) => throw new NotImplementedException();
}
