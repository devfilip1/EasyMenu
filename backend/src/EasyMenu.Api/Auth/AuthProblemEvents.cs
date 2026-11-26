using EasyMenu.Api.ErrorHandling;
using EasyMenu.Application.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Net.Http.Headers;

namespace EasyMenu.Api.Auth;

/// <summary>
/// 401/403 bodies as RFC 7807 problems with <c>code</c> <c>unauthorized</c> / <c>forbidden</c>, so the client can
/// tell an expired session from a missing permission. The Bearer challenge header is kept.
/// </summary>
public static class AuthProblemEvents
{
    public static async Task OnChallenge(JwtBearerChallengeContext context)
    {
        var response = context.Response;
        if (response.HasStarted)
        {
            return; // nothing can be written: let the default handler do what it can
        }

        context.HandleResponse();
        response.StatusCode = StatusCodes.Status401Unauthorized;
        response.Headers.Append(HeaderNames.WWWAuthenticate, BearerChallenge(context));
        await WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized);
    }

    public static Task OnForbidden(ForbiddenContext context)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden);
    }

    // Same shape as the default JwtBearer challenge (RFC 6750): Bearer [error="...", error_description="..."].
    private static string BearerChallenge(JwtBearerChallengeContext context)
    {
        var parameters = new List<string>();
        var error = context.Error ?? (context.AuthenticateFailure is not null ? "invalid_token" : null);
        if (error is not null)
        {
            parameters.Add($"error=\"{error}\"");
        }

        if (context.ErrorDescription is { Length: > 0 } description)
        {
            parameters.Add($"error_description=\"{description}\"");
        }

        return parameters.Count == 0
            ? JwtBearerDefaults.AuthenticationScheme
            : $"{JwtBearerDefaults.AuthenticationScheme} {string.Join(", ", parameters)}";
    }

    private static async Task WriteAsync(HttpContext httpContext, int status, string code)
    {
        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = ProblemDetailsExceptionHandler.Create(status, code),
        });
    }
}
