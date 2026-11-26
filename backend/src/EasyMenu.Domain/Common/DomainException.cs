namespace EasyMenu.Domain.Common;

/// <summary>Violation of a domain rule. <see cref="Code"/> is a stable ErrorCode (snake_case) from the contract.</summary>
public class DomainException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
