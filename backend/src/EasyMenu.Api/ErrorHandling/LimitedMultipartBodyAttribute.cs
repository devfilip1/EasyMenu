using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EasyMenu.Api.ErrorHandling;

/// <summary>
/// Caps a multipart request at <see cref="MaxBytes"/> and reads the form <b>before</b> model binding. MVC's form
/// reader would turn an oversized body into a 400 model error; here it becomes a 413
/// <see cref="BadHttpRequestException"/>, which <see cref="ProblemDetailsExceptionHandler"/> answers with the
/// endpoint's <see cref="PayloadTooLargeCodeAttribute"/> code (research R4).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LimitedMultipartBodyAttribute(long maxBytes) : Attribute, IAsyncResourceFilter
{
    public long MaxBytes { get; } = maxBytes;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        if (httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } sizeFeature)
        {
            sizeFeature.MaxRequestBodySize = MaxBytes;
        }

        if (httpContext.Request.ContentLength > MaxBytes)
        {
            throw TooLarge(null);
        }

        if (httpContext.Request.HasFormContentType)
        {
            var options = new FormOptions { MultipartBodyLengthLimit = MaxBytes };
            httpContext.Features.Set<IFormFeature>(new FormFeature(httpContext.Request, options));
            try
            {
                await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
            }
            catch (InvalidDataException exception)
            {
                throw TooLarge(exception);
            }
        }

        await next();
    }

    private BadHttpRequestException TooLarge(Exception? inner)
    {
        var message = $"Request body larger than {MaxBytes} bytes.";
        return inner is null
            ? new BadHttpRequestException(message, StatusCodes.Status413PayloadTooLarge)
            : new BadHttpRequestException(message, StatusCodes.Status413PayloadTooLarge, inner);
    }
}
