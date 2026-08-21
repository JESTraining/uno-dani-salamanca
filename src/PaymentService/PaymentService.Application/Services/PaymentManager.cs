using PaymentService.Application.Abstractions;
using PaymentService.Application.Contracts;
using PaymentService.Domain;

namespace PaymentService.Application.Services;

public class PaymentManager : IPaymentManager
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGatewayClient _gateway;
    private readonly IPaymentEventPublisher _eventPublisher;

    public PaymentManager(
        IPaymentRepository paymentRepository,
        IUnitOfWork unitOfWork,
        IPaymentGatewayClient gateway,
        IPaymentEventPublisher eventPublisher)
    {
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
        _gateway = gateway;
        _eventPublisher = eventPublisher;
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(Guid orderId, decimal amount, string? paymentMethod, CancellationToken cancellationToken)
    {
        var existing = await _paymentRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (existing is not null)
            return MapToResponse(existing);

        var payment = Payment.Create(orderId, amount, paymentMethod ?? "CreditCard");
        payment.MarkAsProcessing();

        var log = PaymentTransactionLog.Start(payment.Id, attemptNumber: 1, amount);

        await _paymentRepository.AddAsync(payment, cancellationToken);
        await _paymentRepository.AddTransactionLogAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var result = await _gateway.ChargeAsync(amount, cancellationToken);

        if (result.Succeeded)
        {
            payment.MarkAsSucceeded(result.TransactionId!.Value);
            log.Complete(PaymentStatus.Succeeded);
        }
        else
        {
            payment.MarkAsFailed(result.FailureReason!);
            log.Complete(PaymentStatus.Failed, result.FailureReason);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (result.Succeeded)
            await _eventPublisher.PublishPaymentProcessedAsync(payment, cancellationToken);
        else
            await _eventPublisher.PublishPaymentFailedAsync(payment, cancellationToken);

        return MapToResponse(payment);
    }

    public async Task<PaymentResponse?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(orderId, cancellationToken);
        return payment is null ? null : MapToResponse(payment);
    }

    private static PaymentResponse MapToResponse(Payment payment) => new(
        payment.Id,
        payment.OrderId,
        payment.Amount,
        payment.PaymentMethod,
        payment.Status.ToString(),
        payment.TransactionId,
        payment.FailureReason,
        payment.CreatedAt,
        payment.UpdatedAt);
}
