namespace EasyMenu.Domain.Common;

/// <summary>Violation of a domain rule. <see cref="Code"/> is a stable ErrorCode (snake_case) from the contract.</summary>
/// <remarks>Stub created by the test author (T005) so the tests compile; implementation belongs to T010.</remarks>
public class DomainException : Exception
{
    public DomainException(string code) => throw new NotImplementedException();

    public string Code => throw new NotImplementedException();
}
