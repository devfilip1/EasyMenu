using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EasyMenu.Api.ErrorHandling;

/// <summary>
/// Replaces MVC's <c>UnsupportedContentTypeFilter</c>. A request with <b>no body at all</b> (no Content-Type, no
/// content) to an endpoint that requires one is a missing body: 400 <c>validation_failed</c> through the invalid
/// model state response, like <c>{}</c>, instead of 415. A body of an unsupported media type still gets 415.
/// </summary>
public sealed class MissingBodyAwareContentTypeFilter : IActionFilter, IOrderedFilter
{
    public const string MissingBodyMessage = "A non-empty request body is required.";

    /// <summary>Same order as MVC's filter: before the invalid model state filter.</summary>
    public int Order => -3000;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var unsupported = context.ModelState
            .Where(entry => entry.Value!.Errors.Any(e => e.Exception is UnsupportedContentTypeException))
            .Select(entry => entry.Key)
            .ToList();
        if (unsupported.Count == 0)
        {
            return;
        }

        var request = context.HttpContext.Request;
        var hasBody = request.ContentType is not null || request.ContentLength > 0 || request.Headers.TransferEncoding.Count > 0;
        if (hasBody)
        {
            context.Result = new UnsupportedMediaTypeResult();
            return;
        }

        foreach (var key in unsupported)
        {
            context.ModelState.Remove(key);
            context.ModelState.AddModelError(key, MissingBodyMessage);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    /// <summary>Swaps MVC's filter (internal type, added by the MVC core setup) for this one.</summary>
    public static void Register(MvcOptions options)
    {
        var builtIn = options.Filters.Where(f => f.GetType().Name == "UnsupportedContentTypeFilter").ToList();
        foreach (var filter in builtIn)
        {
            options.Filters.Remove(filter);
        }

        options.Filters.Add(new MissingBodyAwareContentTypeFilter());
    }
}
