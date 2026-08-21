using PaymentService.Infrastructure.ExternalServices;

namespace PaymentService.Tests.Infrastructure;

/// Returns a fixed sequence of values so the gateway's random-chance
/// branches are deterministic in tests.
internal sealed class FixedRandomProvider : IRandomProvider
{
    private readonly Queue<double> _values;

    public FixedRandomProvider(params double[] values) => _values = new Queue<double>(values);

    public double NextDouble() => _values.Count > 0 ? _values.Dequeue() : 0.5;
}

internal sealed class NoOpPaymentProcessingDelay : IPaymentProcessingDelay
{
    public Task DelayAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class MockPaymentGatewayTests
{
    private static MockPaymentGateway CreateGateway(params double[] randomValues) =>
        new(new FixedRandomProvider(randomValues), new NoOpPaymentProcessingDelay());

    [Fact]
    public async Task ChargeAsync_AmountOverTenThousand_AlwaysFails()
    {
        var gateway = CreateGateway();

        var result = await gateway.ChargeAsync(10000.01m, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("fraud", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChargeAsync_AmountExactlyTenThousand_Succeeds()
    {
        var gateway = CreateGateway();

        var result = await gateway.ChargeAsync(10000.00m, CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ChargeAsync_EndsInNinetyNineCents_WithLowRandomValue_Fails()
    {
        // Random value below the 0.20 failure threshold triggers the failure branch.
        var gateway = CreateGateway(0.10);

        var result = await gateway.ChargeAsync(49.99m, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ChargeAsync_EndsInNinetyNineCents_WithHighRandomValue_Succeeds()
    {
        // Random value above the 0.20 failure threshold takes the success branch.
        var gateway = CreateGateway(0.99);

        var result = await gateway.ChargeAsync(49.99m, CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ChargeAsync_RegularAmount_Succeeds()
    {
        var gateway = CreateGateway();

        var result = await gateway.ChargeAsync(150.00m, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.TransactionId);
    }
}
