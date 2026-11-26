namespace EasyMenu.Application.Common;

/// <summary>Resource not found or owned by another establishment (mapped to 404 with <see cref="Code"/>).</summary>
/// <remarks>Stub created by the test author (T009) so the tests compile; implementation belongs to T011.</remarks>
public class NotFoundException : Exception
{
    public NotFoundException(string code = "not_found") => throw new NotImplementedException();

    public string Code => throw new NotImplementedException();
}
