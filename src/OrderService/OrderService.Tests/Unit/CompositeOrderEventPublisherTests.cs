using Moq;
using OrderService.Application.Abstractions;
using OrderService.Domain;
using OrderService.Infrastructure.Messaging;

namespace OrderService.Tests.Unit;

public class CompositeOrderEventPublisherTests
{
    private static Order NewOrder() =>
        Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com", new[] { new OrderItem(Guid.NewGuid(), "Widget", 1, 9.99m) });

    [Fact]
    public async Task PublishOrderCreatedAsync_InvokesEveryWrappedPublisherWithTheSameOrder()
    {
        var first = new Mock<IOrderEventPublisher>();
        var second = new Mock<IOrderEventPublisher>();
        var composite = new CompositeOrderEventPublisher(first.Object, second.Object);
        var order = NewOrder();

        await composite.PublishOrderCreatedAsync(order, default);

        first.Verify(p => p.PublishOrderCreatedAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        second.Verify(p => p.PublishOrderCreatedAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishOrderStatusChangedAsync_InvokesEveryWrappedPublisherWithTheSameArguments()
    {
        var first = new Mock<IOrderEventPublisher>();
        var second = new Mock<IOrderEventPublisher>();
        var composite = new CompositeOrderEventPublisher(first.Object, second.Object);
        var orderId = Guid.NewGuid();

        await composite.PublishOrderStatusChangedAsync(orderId, OrderStatus.Pending, OrderStatus.PaymentProcessing, default);

        first.Verify(p => p.PublishOrderStatusChangedAsync(orderId, OrderStatus.Pending, OrderStatus.PaymentProcessing, It.IsAny<CancellationToken>()), Times.Once);
        second.Verify(p => p.PublishOrderStatusChangedAsync(orderId, OrderStatus.Pending, OrderStatus.PaymentProcessing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishOrderCompletedAsync_InvokesEveryWrappedPublisherWithTheSameArguments()
    {
        var first = new Mock<IOrderEventPublisher>();
        var second = new Mock<IOrderEventPublisher>();
        var composite = new CompositeOrderEventPublisher(first.Object, second.Object);
        var orderId = Guid.NewGuid();

        await composite.PublishOrderCompletedAsync(orderId, OrderStatus.Completed, default);

        first.Verify(p => p.PublishOrderCompletedAsync(orderId, OrderStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
        second.Verify(p => p.PublishOrderCompletedAsync(orderId, OrderStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
    }
}
