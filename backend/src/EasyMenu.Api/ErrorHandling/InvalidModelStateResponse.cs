using EasyMenu.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace EasyMenu.Api.ErrorHandling;

/// <summary>
/// Invalid model state (DataAnnotations, malformed JSON, wrong JSON types) → 400 <c>validation_failed</c>
/// with field errors keyed in camelCase.
/// </summary>
public static class InvalidModelStateResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(
            context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest);
        problem.Extensions[ProblemDetailsExceptionHandler.CodeExtension] = ErrorCodes.ValidationFailed;

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}
