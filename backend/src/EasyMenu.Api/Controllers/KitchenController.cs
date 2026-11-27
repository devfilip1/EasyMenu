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

    /// <summary>
    /// Confirms the pickup with the code the customer shows (FR-027a). Body validation (400 validation_failed)
    /// comes first, then 404, 409 order_already_picked_up / order_not_ready, and 400 pickup_code_required /
    /// invalid_pickup_code.
    /// </summary>
    [HttpPut("{orderId:guid}/pickup", Name = "markOrderPickedUp")]
    [ProducesResponseType<KitchenOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict, ApiProblem.MediaType)]
    public async Task<IActionResult> ConfirmPickup(Guid orderId, ConfirmPickupRequest request, CancellationToken cancellationToken) =>
        Ok(await kitchen.ConfirmPickupAsync(orderId, request.PickupCode, cancellationToken));
}
