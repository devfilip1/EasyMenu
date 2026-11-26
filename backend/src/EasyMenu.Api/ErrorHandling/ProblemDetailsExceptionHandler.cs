using EasyMenu.Application.Common;
using EasyMenu.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace EasyMenu.Api.ErrorHandling;

/// <summary>Turns coded exceptions (<see cref="DomainException"/> and subclasses) into RFC 7807 responses with <c>code</c>.</summary>
public sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public const string CodeExtension = "code";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        var status = StatusFor(domainException.Code);
        var problem = domainException is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = ReasonPhrases.GetReasonPhrase(status);
        problem.Extensions[CodeExtension] = domainException.Code;

        httpContext.Response.StatusCode = status;
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
        _ => StatusCodes.Status400BadRequest,
    };
}
