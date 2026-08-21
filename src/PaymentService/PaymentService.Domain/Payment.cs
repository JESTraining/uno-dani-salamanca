namespace PaymentService.Domain;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentMethod { get; private set; } = default!;
    public PaymentStatus Status { get; private set; }
    public Guid? TransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Payment()
    {
    }

    public static Payment Create(Guid orderId, decimal amount, string paymentMethod)
    {
        if (orderId == Guid.Empty)
            throw new InvalidPaymentOperationException("Order id is required to create a payment.");

        if (amount <= 0)
            throw new InvalidPaymentOperationException("Payment amount must be greater than 0.");

        if (string.IsNullOrWhiteSpace(paymentMethod))
            throw new InvalidPaymentOperationException("Payment method is required.");

        var now = DateTime.UtcNow;
        return new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkAsProcessing()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidPaymentOperationException($"Payment in status '{Status}' cannot transition to Processing.");

        Status = PaymentStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsSucceeded(Guid transactionId)
    {
        if (Status != PaymentStatus.Processing)
            throw new InvalidPaymentOperationException($"Payment in status '{Status}' cannot transition to Succeeded.");

        Status = PaymentStatus.Succeeded;
        TransactionId = transactionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string reason)
    {
        if (Status != PaymentStatus.Processing)
            throw new InvalidPaymentOperationException($"Payment in status '{Status}' cannot transition to Failed.");

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }
}
