namespace EasyMenu.Api.ErrorHandling;

/// <summary>
/// Documentation shape of the error responses (contract schema <c>ProblemDetails</c>), used in
/// <c>[ProducesResponseType&lt;ApiProblem&gt;(status, "application/problem+json")]</c>. The actual body is written by
/// <see cref="ProblemDetailsExceptionHandler"/> / the invalid model state factory.
/// </summary>
public sealed class ApiProblem
{
    public string? Type { get; init; }

    public string? Title { get; init; }

    public required int Status { get; init; }

    public string? Detail { get; init; }

    public required ErrorCode Code { get; init; }

    /// <summary>Field errors (camelCase keys), present when <see cref="Code"/> is <c>validation_failed</c>.</summary>
    public IDictionary<string, string[]>? Errors { get; init; }
}

/// <summary>Contract <c>ErrorCode</c> (serialized in snake_case).</summary>
public enum ErrorCode
{
    ValidationFailed,
    InvalidCredentials,
    EmailTaken,
    SlugTaken,
    InvalidSlug,
    NotFound,
    CategoryNotFound,
    CategoryNotEmpty,
    InvalidOrderList,
    InvalidPrice,
    InvalidPromotionalPrice,
    InvalidImageType,
    ImageTooLarge,
    EstablishmentNotFound,
    EmptyCart,
    InvalidQuantity,
    ItemsUnavailable,
    PaymentDeclined,
    OrderNotFound,
    InvalidStatusTransition,
    OrderNotReady,
    PayloadTooLarge,
    RateLimited,
}
