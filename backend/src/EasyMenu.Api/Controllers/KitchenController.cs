using EasyMenu.Api.Auth;
using EasyMenu.Api.ErrorHandling;
using EasyMenu.Application.Orders;
using EasyMenu.Domain.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Api.Controllers;

/// <summary>Kitchen panel (Kitchen staff or the owner, FR-033a).</summary>
[ApiController]
[Route("api/kitchen/orders")]
[Tags("Kitchen")]
[Authorize(Policy = AuthPolicies.KitchenAccess)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized, ApiProblem.MediaType)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden, ApiProblem.MediaType)]
public sealed class KitchenController(KitchenService kitchen) : ControllerBase
{
    [HttpGet(Name = "listKitchenOrders")]
    [ProducesResponseType<IReadOnlyList<KitchenOrderResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await kitchen.ListActiveAsync(cancellationToken));

    [HttpPut("{orderId:guid}/parts/{station}/status", Name = "updateOrderPartStatus")]
    [ProducesResponseType<KitchenOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict, ApiProblem.MediaType)]
    public async Task<IActionResult> UpdatePartStatus(
        Guid orderId, Station station, UpdatePartStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await kitchen.MovePartAsync(orderId, station, request.Status, cancellationToken));

    [HttpPut("{orderId:guid}/pickup", Name = "markOrderPickedUp")]
    [ProducesResponseType<KitchenOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict, ApiProblem.MediaType)]
    public async Task<IActionResult> MarkPickedUp(Guid orderId, CancellationToken cancellationToken) =>
        Ok(await kitchen.MarkPickedUpAsync(orderId, cancellationToken));
}
