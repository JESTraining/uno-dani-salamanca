using InventoryService.Application.Abstractions;
using InventoryService.Infrastructure.Messaging;
using IntegrationEvents;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace InventoryService.Tests.Unit;

/// In-memory MassTransit test harness (no real RabbitMQ) verifying each
/// consumer is wired to the right message and calls the right method with
/// the right arguments - the class of bug (wrong exchange, wrong field
/// mapping) that testing ReservationManager alone would not catch.
public class ConsumerWiringTests
{
    [Fact]
    public async Task OrderCreatedConsumer_RecordsItemsFromTheEvent()
    {
        var reservationManager = new Mock<IReservationManager>();

        await using var provider = new ServiceCollection()
            .AddSingleton(reservationManager.Object)
            .AddMassTransitTestHarness(x => x.AddConsumer<OrderCreatedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await harness.Bus.Publish(new OrderCreatedEvent(orderId, Guid.NewGuid(), 99.99m, new[] { new OrderCreatedEventItem(productId, 3) }, DateTime.UtcNow));

        Assert.True(await harness.Consumed.Any<OrderCreatedEvent>());
        reservationManager.Verify(
            m => m.RecordOrderItemsAsync(
                orderId,
                It.Is<IReadOnlyCollection<OrderItemSnapshotEntry>>(items => items.Count == 1 && items.First().ProductId == productId && items.First().Quantity == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PaymentProcessedConsumer_TriggersReservationForTheOrder()
    {
        var reservationManager = new Mock<IReservationManager>();

        await using var provider = new ServiceCollection()
            .AddSingleton(reservationManager.Object)
            .AddMassTransitTestHarness(x => x.AddConsumer<PaymentProcessedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new PaymentProcessedEvent(orderId, Guid.NewGuid(), 99.99m, DateTime.UtcNow));

        Assert.True(await harness.Consumed.Any<PaymentProcessedEvent>());
        reservationManager.Verify(m => m.ReserveForOrderAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
