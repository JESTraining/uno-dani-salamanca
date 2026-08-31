namespace PaymentService.Domain;

public class PaymentTransactionLog
{
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public int AttemptNumber { get; private set; }
    public PaymentStatus Status { get; private set; }
    public decimal Amount { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime ProcessingStartedAt { get; private set; }
    public DateTime? ProcessingCompletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PaymentTransactionLog()
    {
    }

    public static PaymentTransactionLog Start(Guid paymentId, int attemptNumber, decimal amount)
    {
        var now = DateTime.UtcNow;
        return new PaymentTransactionLog
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            AttemptNumber = attemptNumber,
            Status = PaymentStatus.Processing,
            Amount = amount,
            ProcessingStartedAt = now,
            CreatedAt = now
        };
    }

    public void Complete(PaymentStatus finalStatus, string? failureReason = null)
    {
        Status = finalStatus;
        FailureReason = failureReason;
        ProcessingCompletedAt = DateTime.UtcNow;
    }
}
