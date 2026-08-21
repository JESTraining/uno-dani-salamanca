using PaymentService.Application.Abstractions;

namespace PaymentService.Infrastructure.ExternalServices;

/// Simulated payment gateway. Rules fixed in CLAUDE.md ("Critical Business
/// Rules") and in the original exercise statement:
/// - Amounts over $10,000 always fail (fraud detection).
/// - Amounts ending in .99 have a 20% random chance of failing.
/// - Everything else succeeds.
public class MockPaymentGateway : IPaymentGatewayClient
{
    private const decimal FraudThreshold = 10000m;
    private const double NearWholeDollarFailureChance = 0.20;

    private readonly IRandomProvider _random;
    private readonly IPaymentProcessingDelay _delay;

    public MockPaymentGateway(IRandomProvider random, IPaymentProcessingDelay delay)
    {
        _random = random;
        _delay = delay;
    }

    public async Task<GatewayChargeResult> ChargeAsync(decimal amount, CancellationToken cancellationToken)
    {
        await _delay.DelayAsync(cancellationToken);

        if (amount > FraudThreshold)
            return new GatewayChargeResult(false, null, "Payment declined: amount exceeds the fraud detection threshold of $10,000.");

        if (EndsInNinetyNineCents(amount) && _random.NextDouble() < NearWholeDollarFailureChance)
            return new GatewayChargeResult(false, null, "Payment declined by issuing bank.");

        return new GatewayChargeResult(true, Guid.NewGuid(), null);
    }

    private static bool EndsInNinetyNineCents(decimal amount)
    {
        var cents = Math.Round(amount - Math.Floor(amount), 2);
        return cents == 0.99m;
    }
}
