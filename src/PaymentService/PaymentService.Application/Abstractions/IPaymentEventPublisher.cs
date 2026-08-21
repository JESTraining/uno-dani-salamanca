using PaymentService.Domain;

namespace PaymentService.Application.Abstractions;

public interface IPaymentEventPublisher
{
    Task PublishPaymentProcessedAsync(Payment payment, CancellationToken cancellationToken);

    Task PublishPaymentFailedAsync(Payment payment, CancellationToken cancellationToken);
}
