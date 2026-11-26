using EasyMenu.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only endpoint protected by the <c>OwnerOnly</c> policy (AuthErrorBodyTests). Registered only in
/// <see cref="EasyMenuApiFactory.WithTestControllers"/>, so it never shows up in /openapi/v1.json.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("__tests/owner-only")]
[Authorize(Policy = AuthPolicies.OwnerOnly)]
public sealed class OwnerOnlyTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { ok = true });
}
