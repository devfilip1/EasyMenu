namespace EasyMenu.Application.Common;

/// <summary>Stable values of the ProblemDetails <c>code</c> extension (contract ErrorCode).</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string InvalidCredentials = "invalid_credentials";
    public const string EmailTaken = "email_taken";
    public const string SlugTaken = "slug_taken";
    public const string NotFound = "not_found";
}
