using System.ComponentModel.DataAnnotations;
using EasyMenu.Api.Auth;
using EasyMenu.Api.ErrorHandling;
using EasyMenu.Application.Orders;
using EasyMenu.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Tags("Orders")]
[Authorize(Policy = AuthPolicies.OwnerOnly)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status401Unauthorized, ApiProblem.MediaType)]
[ProducesResponseType<ApiProblem>(StatusCodes.Status403Forbidden, ApiProblem.MediaType)]
public sealed class OrdersController(OrderQueryService orders) : ControllerBase
{
    [HttpGet(Name = "listOrders")]
    [ProducesResponseType<OrderSummaryPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    public async Task<IActionResult> List(
        [FromQuery] OrderStatus? status,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, OrderQueryService.MaxPageSize)] int pageSize = OrderQueryService.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        Ok(await orders.ListAsync(status, page, pageSize, cancellationToken));

    [HttpGet("{orderId:guid}", Name = "getOrder")]
    [ProducesResponseType<OrderDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> Get(Guid orderId, CancellationToken cancellationToken) =>
        Ok(await orders.GetAsync(orderId, cancellationToken));
}
