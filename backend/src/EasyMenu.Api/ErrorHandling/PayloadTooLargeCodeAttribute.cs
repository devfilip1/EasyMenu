namespace EasyMenu.Api.ErrorHandling;

/// <summary>
/// Error code used when the server rejects the request body of this endpoint as too large (413). Without it the
/// code is <c>payload_too_large</c> (research R4).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PayloadTooLargeCodeAttribute(string code) : Attribute
{
    public const string DefaultCode = "payload_too_large";

    public string Code { get; } = code;
}
