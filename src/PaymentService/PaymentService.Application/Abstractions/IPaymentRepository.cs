using PaymentService.Domain;

namespace PaymentService.Application.Abstractions;

public interface IPaymentRepository
{
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    Task AddTransactionLogAsync(PaymentTransactionLog log, CancellationToken cancellationToken);
}
