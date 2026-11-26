using EasyMenu.Api.ErrorHandling;
using EasyMenu.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Tags("PublicOrders")]
public sealed class PublicOrdersController(CheckoutService checkout) : ControllerBase
{
    [HttpPost("api/public/establishments/{slug}/orders", Name = "placeOrder")]
    [ProducesResponseType<PublicOrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status400BadRequest, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status402PaymentRequired, ApiProblem.MediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    [ProducesResponseType<ItemsUnavailableProblem>(StatusCodes.Status409Conflict, ApiProblem.MediaType)]
    public async Task<IActionResult> PlaceOrder(string slug, PlaceOrderRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await checkout.PlaceAsync(slug, request, cancellationToken));

    [HttpGet("api/public/orders/{publicToken}", Name = "getPublicOrder")]
    [ProducesResponseType<PublicOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound, ApiProblem.MediaType)]
    public async Task<IActionResult> Get(string publicToken, CancellationToken cancellationToken) =>
        Ok(await checkout.GetByPublicTokenAsync(publicToken, cancellationToken));
}
