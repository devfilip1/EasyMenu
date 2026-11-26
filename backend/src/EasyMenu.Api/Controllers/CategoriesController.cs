using EasyMenu.Api.Auth;
using EasyMenu.Api.ErrorHandling;
using EasyMenu.Application.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Tags("Categories")]
[Authorize(Policy = AuthPolicies.OwnerOnly)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized, ApiProblem.MediaType)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden, ApiProblem.MediaType)]
public sealed class CategoriesController(CategoryService categories, MenuItemService items) : ControllerBase
{
    [HttpGet(Name = "listCategories")]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await categories.ListAsync(cancellationToken));

    [HttpPost(Name = "createCategory")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    public async Task<IActionResult> Create(CategoryRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await categories.CreateAsync(request, cancellationToken));

    // Literal segment: must not be captured by the {id:guid} routes below.
    [HttpPut("order", Name = "reorderCategories")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken cancellationToken)
    {
        await categories.ReorderAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}", Name = "renameCategory")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> Rename(Guid id, CategoryRequest request, CancellationToken cancellationToken) =>
        Ok(await categories.RenameAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}", Name = "deleteCategory")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict, ApiProblem.MediaType)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await categories.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/items/order", Name = "reorderCategoryItems")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> ReorderItems(Guid id, ReorderRequest request, CancellationToken cancellationToken)
    {
        await items.ReorderAsync(id, request, cancellationToken);
        return NoContent();
    }
}
