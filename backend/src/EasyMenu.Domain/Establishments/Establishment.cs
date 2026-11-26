namespace EasyMenu.Domain.Establishments;

/// <remarks>Stub created by the test author (T005) so the tests compile; implementation belongs to T010.</remarks>
public sealed class Establishment
{
    private Establishment()
    {
    }

    public Guid Id => throw new NotImplementedException();

    public string Name => throw new NotImplementedException();

    public string Slug => throw new NotImplementedException();

    public DateTimeOffset CreatedAt => throw new NotImplementedException();

    public static Establishment Create(string name, Slug slug, DateTimeOffset createdAt) =>
        throw new NotImplementedException();

    public void Rename(string name) => throw new NotImplementedException();
}
