namespace PaymentService.Infrastructure.ExternalServices;

/// Abstracts randomness so the gateway's 20%-failure-chance rule is
/// deterministic in tests.
public interface IRandomProvider
{
    double NextDouble();
}

public class SystemRandomProvider : IRandomProvider
{
    public double NextDouble() => Random.Shared.NextDouble();
}
