using EasyMenu.Application.Common;
using EasyMenu.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace EasyMenu.Api.ErrorHandling;

/// <summary>
/// Turns coded exceptions (<see cref="DomainException"/> and subclasses) and request bodies rejected as too large
/// into RFC 7807 responses with <c>code</c>.
/// </summary>
public sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public const string CodeExtension = "code";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            DomainException domain => FromDomain(domain),
            // A race that slipped past the application checks; unknown constraints stay a logged 500.
            UniqueConstraintViolationException { ConstraintName: var name } when UniqueConstraints.ErrorCodeFor(name) is { } code
                => Create(StatusFor(code), code),
            _ when FindPayloadTooLarge(exception) is not null => PayloadTooLarge(httpContext),
            _ => null,
        };
        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>Error code → HTTP status (research R10).</summary>
    public static int StatusFor(string code) => code switch
    {
        ErrorCodes.InvalidCredentials => StatusCodes.Status401Unauthorized,
        "payment_declined" => StatusCodes.Status402PaymentRequired,
        ErrorCodes.NotFound => StatusCodes.Status404NotFound,
        _ when code.EndsWith("_not_found", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
        "category_not_empty" or "items_unavailable" or "invalid_status_transition" or "order_not_ready"
            => StatusCodes.Status409Conflict,
        _ when code.EndsWith("_taken", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
        "image_too_large" or PayloadTooLargeCodeAttribute.DefaultCode => StatusCodes.Status413PayloadTooLarge,
        _ => StatusCodes.Status400BadRequest,
    };

    public static ProblemDetails Create(int status, string code, ProblemDetails? problem = null)
    {
        problem ??= new ProblemDetails();
        problem.Status = status;
        problem.Title = ReasonPhrases.GetReasonPhrase(status);
        problem.Extensions[CodeExtension] = code;
        return problem;
    }

    private static ProblemDetails FromDomain(DomainException exception) => Create(
        StatusFor(exception.Code),
        exception.Code,
        exception is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            : null);

    // The server rejects a body above the endpoint limit while it is being read (possibly wrapped by MVC's form
    // reader), so look through the inner exceptions.
    private static BadHttpRequestException? FindPayloadTooLarge(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } tooLarge)
            {
                return tooLarge;
            }
        }

        return null;
    }

    private static ProblemDetails PayloadTooLarge(HttpContext httpContext)
    {
        // The middleware clears the request's endpoint before calling handlers; the feature keeps the original.
        var endpoint = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? httpContext.GetEndpoint();
        var code = endpoint?.Metadata.GetMetadata<PayloadTooLargeCodeAttribute>()?.Code
            ?? PayloadTooLargeCodeAttribute.DefaultCode;
        return Create(StatusCodes.Status413PayloadTooLarge, code);
    }
}
