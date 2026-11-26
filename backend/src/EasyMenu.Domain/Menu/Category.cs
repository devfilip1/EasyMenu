namespace EasyMenu.Domain.Menu;

/// <summary>Menu category of an establishment, ordered by <see cref="Position"/>.</summary>
/// <remarks>Stub created by the test author (T021) so the tests compile; implementation belongs to T023.</remarks>
public sealed class Category
{
    private Category()
    {
    }

    public Guid Id => throw new NotImplementedException();

    public Guid EstablishmentId => throw new NotImplementedException();

    public string Name => throw new NotImplementedException();

    public int Position => throw new NotImplementedException();

    public static Category Create(Guid establishmentId, string name, int position) => throw new NotImplementedException();

    public void Rename(string name) => throw new NotImplementedException();

    public void MoveTo(int position) => throw new NotImplementedException();
}
