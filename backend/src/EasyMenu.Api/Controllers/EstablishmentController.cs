using EasyMenu.Api.Auth;
using EasyMenu.Api.ErrorHandling;
using EasyMenu.Application.Auth;
using EasyMenu.Application.Establishments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Api.Controllers;

[ApiController]
[Route("api/establishment")]
[Tags("Establishment")]
[Authorize(Policy = AuthPolicies.OwnerOnly)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized, ApiProblem.MediaType)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden, ApiProblem.MediaType)]
public sealed class EstablishmentController(EstablishmentService establishments) : ControllerBase
{
    [HttpGet(Name = "getEstablishment")]
    [ProducesResponseType<EstablishmentResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await establishments.GetAsync(cancellationToken));

    [HttpPut(Name = "updateEstablishment")]
    [ProducesResponseType<EstablishmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    public async Task<IActionResult> Update(UpdateEstablishmentRequest request, CancellationToken cancellationToken) =>
        Ok(await establishments.RenameAsync(request, cancellationToken));
}
