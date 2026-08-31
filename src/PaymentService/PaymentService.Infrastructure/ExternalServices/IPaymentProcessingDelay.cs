namespace PaymentService.Infrastructure.ExternalServices;

/// Abstracts the simulated processing delay so tests do not have to wait
/// 2-5 real seconds per payment. Production DI registers
/// RandomPaymentProcessingDelay; tests substitute a no-op.
public interface IPaymentProcessingDelay
{
    Task DelayAsync(CancellationToken cancellationToken);
}

public class RandomPaymentProcessingDelay : IPaymentProcessingDelay
{
    public Task DelayAsync(CancellationToken cancellationToken)
    {
        var seconds = 2 + Random.Shared.NextDouble() * 3;
        return Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
    }
}
