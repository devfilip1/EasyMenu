using EasyMenu.Api.Auth;
using EasyMenu.Api.ErrorHandling;
using EasyMenu.Application.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Api.Controllers;

[ApiController]
[Route("api/staff")]
[Tags("Staff")]
[Authorize(Policy = AuthPolicies.OwnerOnly)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized, ApiProblem.MediaType)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden, ApiProblem.MediaType)]
public sealed class StaffController(StaffService staff) : ControllerBase
{
    [HttpGet(Name = "listStaff")]
    [ProducesResponseType<IReadOnlyList<StaffMemberResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await staff.ListAsync(cancellationToken));

    [HttpPost(Name = "createStaffMember")]
    [ProducesResponseType<StaffMemberResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict, ApiProblem.MediaType)]
    public async Task<IActionResult> Create(CreateStaffMemberRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await staff.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}/deactivate", Name = "deactivateStaffMember")]
    [ProducesResponseType<StaffMemberResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await staff.DeactivateAsync(id, cancellationToken));
}
