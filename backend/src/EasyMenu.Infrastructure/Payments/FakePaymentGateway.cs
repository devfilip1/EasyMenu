using EasyMenu.Application.Abstractions;

namespace EasyMenu.Infrastructure.Payments;

/// <summary>Simulated payment (MVP): every charge is approved, without any payment data.</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentResult(Approved: true, TransactionId: $"fake-{Guid.NewGuid():N}"));
}
