namespace EasyMenu.Application.Abstractions;

/// <summary>Charges an order (constitution V: simulated in the MVP, behind this interface for a real gateway later).</summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken);
}

public sealed record PaymentRequest(Guid EstablishmentId, Guid OrderId, long AmountCents);

public sealed record PaymentResult(bool Approved, string? TransactionId);
