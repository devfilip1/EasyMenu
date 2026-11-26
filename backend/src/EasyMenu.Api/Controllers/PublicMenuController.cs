using EasyMenu.Api.ErrorHandling;
using EasyMenu.Application.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/establishments/{slug}/menu")]
[Tags("PublicMenu")]
public sealed class PublicMenuController(PublicMenuService publicMenu) : ControllerBase
{
    [HttpGet(Name = "getPublicMenu")]
    [ProducesResponseType<PublicMenuResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> Get(string slug, CancellationToken cancellationToken) =>
        Ok(await publicMenu.GetAsync(slug, cancellationToken));
}
