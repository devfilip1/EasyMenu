using EasyMenu.Api.ErrorHandling;
using EasyMenu.Api.RateLimiting;
using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EasyMenu.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public sealed class AuthController(AuthService authService, ITenantContext tenant) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register-owner", Name = "registerOwner")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status429TooManyRequests, ApiProblem.MediaType)]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    public async Task<IActionResult> RegisterOwner(RegisterOwnerRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.RegisterOwnerAsync(request, cancellationToken);
        return Created("/api/auth/me", response);
    }

    [AllowAnonymous]
    [HttpPost("login", Name = "login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status429TooManyRequests, ApiProblem.MediaType)]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.LoginAsync(request, cancellationToken));

    [Authorize]
    [HttpGet("me", Name = "getCurrentUser")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized, ApiProblem.MediaType)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = tenant.UserId ?? throw new InvalidOperationException("Authenticated request without a user id.");
        return Ok(await authService.GetCurrentUserAsync(userId, cancellationToken));
    }
}
