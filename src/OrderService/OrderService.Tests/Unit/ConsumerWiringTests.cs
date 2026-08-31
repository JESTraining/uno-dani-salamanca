using IntegrationEvents;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrderService.Application.Abstractions;
using OrderService.Infrastructure.Messaging;

namespace OrderService.Tests.Unit;

/// In-memory MassTransit test harness (no real RabbitMQ) verifying each of
/// the four saga consumers is wired to the right message and calls the
/// right IOrderManager method with the right arguments extracted from the
/// event - the class of bug (wrong exchange, wrong field mapping) that
/// testing OrderManager alone would not catch.
public class ConsumerWiringTests
{
    [Fact]
    public async Task PaymentProcessedConsumer_MarksPaymentProcessedForTheOrder()
    {
        var orderManager = new Mock<IOrderManager>();

        await using var provider = new ServiceCollection()
            .AddSingleton(orderManager.Object)
            .AddMassTransitTestHarness(x => x.AddConsumer<PaymentProcessedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new PaymentProcessedEvent(orderId, Guid.NewGuid(), 99.99m, DateTime.UtcNow));

        Assert.True(await harness.Consumed.Any<PaymentProcessedEvent>());
        orderManager.Verify(m => m.MarkPaymentProcessedAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaymentFailedConsumer_MarksPaymentFailedWithTheReason()
    {
        var orderManager = new Mock<IOrderManager>();

        await using var provider = new ServiceCollection()
            .AddSingleton(orderManager.Object)
            .AddMassTransitTestHarness(x => x.AddConsumer<PaymentFailedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new PaymentFailedEvent(orderId, "fraud detection threshold exceeded", DateTime.UtcNow));

        Assert.True(await harness.Consumed.Any<PaymentFailedEvent>());
        orderManager.Verify(m => m.MarkPaymentFailedAsync(orderId, "fraud detection threshold exceeded", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InventoryReservedConsumer_CompletesTheOrder()
    {
        var orderManager = new Mock<IOrderManager>();

        await using var provider = new ServiceCollection()
            .AddSingleton(orderManager.Object)
            .AddMassTransitTestHarness(x => x.AddConsumer<InventoryReservedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new InventoryReservedEvent(orderId, new[] { new InventoryReservedEventItem(Guid.NewGuid(), 2) }, DateTime.UtcNow));

        Assert.True(await harness.Consumed.Any<InventoryReservedEvent>());
        orderManager.Verify(m => m.CompleteOrderAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InventoryFailedConsumer_MarksInventoryFailedWithTheReason()
    {
        var orderManager = new Mock<IOrderManager>();

        await using var provider = new ServiceCollection()
            .AddSingleton(orderManager.Object)
            .AddMassTransitTestHarness(x => x.AddConsumer<InventoryFailedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new InventoryFailedEvent(orderId, "insufficient stock for product X", DateTime.UtcNow));

        Assert.True(await harness.Consumed.Any<InventoryFailedEvent>());
        orderManager.Verify(m => m.MarkInventoryFailedAsync(orderId, "insufficient stock for product X", It.IsAny<CancellationToken>()), Times.Once);
    }
}
