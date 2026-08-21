namespace PaymentService.Application.Abstractions;

public sealed record GatewayChargeResult(bool Succeeded, Guid? TransactionId, string? FailureReason);

/// Port towards the payment processor. The real gateway is out of scope for
/// this exercise; the Infrastructure implementation is a mock that follows
/// the fraud/failure rules fixed in CLAUDE.md.
public interface IPaymentGatewayClient
{
    Task<GatewayChargeResult> ChargeAsync(decimal amount, CancellationToken cancellationToken);
}
