using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Domain;
using OrderService.Infrastructure.Messaging;

namespace OrderService.Tests.Unit;

public class SignalROrderEventPublisherTests
{
    private static Order NewOrder() =>
        Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com", new[] { new OrderItem(Guid.NewGuid(), "Widget", 1, 9.99m) });

    private static (SignalROrderEventPublisher Publisher, Mock<IClientProxy> ClientProxy) CreatePublisher()
    {
        var clientProxy = new Mock<IClientProxy>();
        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.All).Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<OrderHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var publisher = new SignalROrderEventPublisher(hubContext.Object, NullLogger<SignalROrderEventPublisher>.Instance);
        return (publisher, clientProxy);
    }

    [Fact]
    public async Task PublishOrderCreatedAsync_BroadcastsOrderCreatedToAllClients()
    {
        var (publisher, clientProxy) = CreatePublisher();
        var order = NewOrder();

        await publisher.PublishOrderCreatedAsync(order, default);

        clientProxy.Verify(
            c => c.SendCoreAsync("OrderCreated", It.Is<object[]>(args => args.Length == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishOrderStatusChangedAsync_BroadcastsOrderStatusChangedToAllClients()
    {
        var (publisher, clientProxy) = CreatePublisher();

        await publisher.PublishOrderStatusChangedAsync(Guid.NewGuid(), OrderStatus.Pending, OrderStatus.PaymentProcessing, default);

        clientProxy.Verify(
            c => c.SendCoreAsync("OrderStatusChanged", It.Is<object[]>(args => args.Length == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishOrderCompletedAsync_BroadcastsOrderCompletedToAllClients()
    {
        var (publisher, clientProxy) = CreatePublisher();

        await publisher.PublishOrderCompletedAsync(Guid.NewGuid(), OrderStatus.Completed, default);

        clientProxy.Verify(
            c => c.SendCoreAsync("OrderCompleted", It.Is<object[]>(args => args.Length == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishOrderCompletedAsync_WhenBroadcastThrows_SwallowsTheExceptionInsteadOfPropagating()
    {
        var clientProxy = new Mock<IClientProxy>();
        clientProxy
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated SignalR transport failure."));
        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.All).Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<OrderHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);
        var publisher = new SignalROrderEventPublisher(hubContext.Object, NullLogger<SignalROrderEventPublisher>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            publisher.PublishOrderCompletedAsync(Guid.NewGuid(), OrderStatus.Completed, default));

        Assert.Null(exception);
    }
}
