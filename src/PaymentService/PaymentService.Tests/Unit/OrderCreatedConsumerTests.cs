using IntegrationEvents;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Contracts;
using PaymentService.Infrastructure.Messaging;

namespace PaymentService.Tests.Unit;

/// Uses MassTransit's in-memory test harness (no real RabbitMQ) to verify
/// the consumer is actually wired to the message and extracts the right
/// fields - the kind of bug (wrong exchange, wrong field mapping) that
/// unit-testing PaymentManager alone would not catch.
public class OrderCreatedConsumerTests
{
    [Fact]
    public async Task Consume_OrderCreatedEvent_CallsProcessPaymentAsyncWithOrderIdAndTotalAmount()
    {
        var paymentManager = new Mock<IPaymentManager>();
        paymentManager
            .Setup(m => m.ProcessPaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResponse(Guid.NewGuid(), Guid.NewGuid(), 0m, "CreditCard", "Succeeded", Guid.NewGuid(), null, DateTime.UtcNow, DateTime.UtcNow));

        await using var provider = new ServiceCollection()
            .AddSingleton(paymentManager.Object)
            .AddMassTransitTestHarness(x => x.AddConsumer<OrderCreatedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new OrderCreatedEvent(orderId, Guid.NewGuid(), 249.99m, Array.Empty<OrderCreatedEventItem>(), DateTime.UtcNow));

        Assert.True(await harness.Consumed.Any<OrderCreatedEvent>());
        paymentManager.Verify(m => m.ProcessPaymentAsync(orderId, 249.99m, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
