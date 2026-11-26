using EasyMenu.Domain.Common;

namespace EasyMenu.Application.Common;

/// <summary>Input rejected by an application rule; mapped to 400 <c>validation_failed</c> with per-field errors (camelCase keys).</summary>
public class ValidationException(IReadOnlyDictionary<string, string[]> errors) : DomainException(ErrorCodes.ValidationFailed)
{
    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
