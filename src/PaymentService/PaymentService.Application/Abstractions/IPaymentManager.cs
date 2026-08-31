using PaymentService.Application.Contracts;

namespace PaymentService.Application.Abstractions;

public interface IPaymentManager
{
    Task<PaymentResponse> ProcessPaymentAsync(Guid orderId, decimal amount, string? paymentMethod, CancellationToken cancellationToken);

    Task<PaymentResponse?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
}
