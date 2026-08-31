using IntegrationEvents;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrderService.Application.Abstractions;
using OrderService.Infrastructure.Messaging;

namespace OrderService.Tests.Unit;

/// Proves MassTransit's retry-then-fault mechanism (Task 3.1: 3 retries with
/// exponential backoff, then dead-letter) actually engages when a consumer
/// keeps throwing. Uses the in-memory test transport, so this proves the
/// retry/fault mechanism itself, not a literal RabbitMQ "_error" queue -
/// that part is only provable manually against real RabbitMQ (see plan/03,
/// Verification).
public class RetryExhaustionTests
{
    [Fact]
    public async Task Consumer_ThatAlwaysThrows_RetriesThreeTimesThenFaults()
    {
        var orderManager = new Mock<IOrderManager>();
        orderManager
            .Setup(m => m.MarkPaymentProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated persistent failure."));

        await using var provider = new ServiceCollection()
            .AddSingleton(orderManager.Object)
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<PaymentProcessedConsumer>();
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
                    cfg.ConfigureEndpoints(context);
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new PaymentProcessedEvent(orderId, Guid.NewGuid(), 99.99m, DateTime.UtcNow));

        Assert.True(await harness.Published.Any<Fault<PaymentProcessedEvent>>());
        orderManager.Verify(
            m => m.MarkPaymentProcessedAsync(orderId, It.IsAny<CancellationToken>()),
            Times.Exactly(4)); // 1 original attempt + 3 retries
    }
}
