using System.ComponentModel.DataAnnotations;
using EasyMenu.Api.Auth;
using EasyMenu.Api.ErrorHandling;
using EasyMenu.Application.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Api.Controllers;

[ApiController]
[Route("api/menu-items")]
[Tags("MenuItems")]
[Authorize(Policy = AuthPolicies.OwnerOnly)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized, ApiProblem.MediaType)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden, ApiProblem.MediaType)]
public sealed class MenuItemsController(MenuItemService items) : ControllerBase
{
    [HttpGet(Name = "listMenuItems")]
    [ProducesResponseType<IReadOnlyList<MenuItemResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? categoryId, CancellationToken cancellationToken) =>
        Ok(await items.ListAsync(categoryId, cancellationToken));

    [HttpPost(Name = "createMenuItem")]
    [ProducesResponseType<MenuItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> Create(MenuItemRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await items.CreateAsync(request, cancellationToken));

    [HttpGet("{id:guid}", Name = "getMenuItem")]
    [ProducesResponseType<MenuItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await items.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}", Name = "updateMenuItem")]
    [ProducesResponseType<MenuItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> Update(Guid id, MenuItemRequest request, CancellationToken cancellationToken) =>
        Ok(await items.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}", Name = "deleteMenuItem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await items.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/availability", Name = "setMenuItemAvailability")]
    [ProducesResponseType<MenuItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> SetAvailability(Guid id, AvailabilityRequest request, CancellationToken cancellationToken) =>
        Ok(await items.SetAvailabilityAsync(id, request, cancellationToken));

    /// <summary>Request limit: 6 MB, so a 5 MB photo plus multipart overhead fits (research R4).</summary>
    private const long PhotoRequestLimitBytes = 6 * 1024 * 1024;

    [HttpPut("{id:guid}/photo", Name = "uploadMenuItemPhoto")]
    [Consumes("multipart/form-data")]
    [LimitedMultipartBody(PhotoRequestLimitBytes)]
    [PayloadTooLargeCode(MenuItemService.ImageTooLarge)]
    [ProducesResponseType<MenuItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status413PayloadTooLarge, ApiProblem.MediaType)]
    public async Task<IActionResult> UploadPhoto(Guid id, [FromForm] PhotoUploadForm form, CancellationToken cancellationToken)
    {
        await using var content = form.File.OpenReadStream();
        return Ok(await items.SetPhotoAsync(id, content, form.File.Length, cancellationToken));
    }

    /// <summary>Multipart body of the photo upload (contract: required field <c>file</c>).</summary>
    public sealed class PhotoUploadForm
    {
        [Required]
        [FromForm(Name = "file")]
        public IFormFile File { get; set; } = null!;
    }

    [HttpDelete("{id:guid}/photo", Name = "deleteMenuItemPhoto")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> DeletePhoto(Guid id, CancellationToken cancellationToken)
    {
        await items.DeletePhotoAsync(id, cancellationToken);
        return NoContent();
    }
}
