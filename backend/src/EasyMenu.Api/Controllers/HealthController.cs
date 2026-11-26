using EasyMenu.Api.Contracts;
using EasyMenu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet(Name = "getHealth")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Ok(new HealthResponse(HealthStatus.Healthy))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse(HealthStatus.Unhealthy));
    }
}
