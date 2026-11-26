using System.ComponentModel.DataAnnotations;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only controller used by ErrorHandlingTests. It is registered only in the host returned by
/// <see cref="EasyMenuApiFactory.WithTestControllers"/>, never in the host compared against the contract.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("__tests/errors")]
public sealed class ErrorTestController : ControllerBase
{
    [HttpGet("domain/{code}")]
    public IActionResult ThrowDomain(string code) => throw new DomainException(code);

    [HttpGet("not-found")]
    public IActionResult ThrowNotFound() => throw new NotFoundException();

    [HttpPost("validate")]
    public IActionResult Validate(ValidatedModel model) => Ok(model);

    [HttpGet("serialization")]
    public ActionResult<SerializationSample> Serialization() =>
        new SerializationSample(SampleStatus.ReadyForPickup, Note: null, AmountCents: 1999);

    [HttpPost("serialization")]
    public ActionResult<SerializationSample> Echo(SerializationSample sample) => sample;
}

public sealed class ValidatedModel
{
    [Required]
    [StringLength(10)]
    public string? FirstName { get; set; }

    [Range(1, 10)]
    public int Quantity { get; set; }
}

public enum SampleStatus
{
    AwaitingPayment,
    ReadyForPickup,
}

public sealed record SerializationSample(SampleStatus Status, string? Note, long AmountCents);
