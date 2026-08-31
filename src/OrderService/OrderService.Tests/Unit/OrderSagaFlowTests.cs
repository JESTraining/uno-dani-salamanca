using IntegrationEvents;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Application.Abstractions;
using OrderService.Application.Contracts;
using OrderService.Application.Services;
using OrderService.Domain;
using OrderService.Infrastructure.Messaging;

namespace OrderService.Tests.Unit;

/// End-to-end, in-memory proof of the choreographed saga (Phase 3): a real
/// OrderManager (not mocked) wired to the four real consumers via MassTransit's
/// in-memory test harness, driving an order through every branch of the flow
/// (Pending -> PaymentProcessing -> InventoryProcessing -> Completed, and the
/// two failure branches), plus a duplicate-event idempotency check.
public class OrderSagaFlowTests
{
    private sealed class InMemoryOrderRepository : IOrderRepository
    {
        private readonly Dictionary<Guid, Order> _orders = new();

        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_orders.GetValueOrDefault(id));

        public Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(OrderListQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not needed for saga flow tests.");

        public Task AddAsync(Order order, CancellationToken cancellationToken)
        {
            _orders[order.Id] = order;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private static CreateOrderRequest ValidRequest() => new(
        Guid.NewGuid(), "Jane Doe", "jane@example.com",
        new[] { new CreateOrderItemRequest(Guid.NewGuid(), "Widget", 1, 9.99m) });

    private static (IOrderManager Manager, InMemoryOrderRepository Repository, Mock<IOrderEventPublisher> Publisher) CreateManager()
    {
        var repository = new InMemoryOrderRepository();
        var publisher = new Mock<IOrderEventPublisher>();
        var inventoryChecker = new Mock<IInventoryAvailabilityChecker>();
        inventoryChecker
            .Setup(c => c.IsStockAvailableAsync(It.IsAny<IReadOnlyCollection<StockCheckItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var idempotencyStore = new Mock<IIdempotencyStore>();

        IOrderManager manager = new OrderManager(
            repository,
            new NoOpUnitOfWork(),
            inventoryChecker.Object,
            publisher.Object,
            idempotencyStore.Object,
            NullLogger<OrderManager>.Instance);

        return (manager, repository, publisher);
    }

    [Fact]
    public async Task HappyPath_PaymentProcessedThenInventoryReserved_ReachesCompletedAndPublishesOrderCompleted()
    {
        var (manager, repository, publisher) = CreateManager();
        var created = await manager.CreateOrderAsync(ValidRequest(), null, default);
        Assert.Equal("PaymentProcessing", created.Status);

        await using var provider = new ServiceCollection()
            .AddSingleton(manager)
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<PaymentProcessedConsumer>();
                x.AddConsumer<InventoryReservedConsumer>();
            })
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new PaymentProcessedEvent(created.Id, Guid.NewGuid(), created.TotalAmount, DateTime.UtcNow));
        Assert.True(await harness.Consumed.Any<PaymentProcessedEvent>());
        Assert.Equal(OrderStatus.InventoryProcessing, (await repository.GetByIdAsync(created.Id, default))!.Status);

        await harness.Bus.Publish(new InventoryReservedEvent(created.Id, new[] { new InventoryReservedEventItem(Guid.NewGuid(), 1) }, DateTime.UtcNow));
        Assert.True(await harness.Consumed.Any<InventoryReservedEvent>());

        Assert.Equal(OrderStatus.Completed, (await repository.GetByIdAsync(created.Id, default))!.Status);
        publisher.Verify(p => p.PublishOrderCompletedAsync(created.Id, OrderStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaymentFailurePath_ReachesPaymentFailedAndNeverPublishesOrderCompleted()
    {
        var (manager, repository, publisher) = CreateManager();
        var created = await manager.CreateOrderAsync(ValidRequest(), null, default);

        await using var provider = new ServiceCollection()
            .AddSingleton(manager)
            .AddMassTransitTestHarness(x => x.AddConsumer<PaymentFailedConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new PaymentFailedEvent(created.Id, "fraud detection threshold exceeded", DateTime.UtcNow));
        Assert.True(await harness.Consumed.Any<PaymentFailedEvent>());

        Assert.Equal(OrderStatus.PaymentFailed, (await repository.GetByIdAsync(created.Id, default))!.Status);
        publisher.Verify(p => p.PublishOrderCompletedAsync(It.IsAny<Guid>(), It.IsAny<OrderStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InventoryFailurePath_ReachesInventoryFailedAndNeverPublishesOrderCompleted()
    {
        var (manager, repository, publisher) = CreateManager();
        var created = await manager.CreateOrderAsync(ValidRequest(), null, default);

        await using var provider = new ServiceCollection()
            .AddSingleton(manager)
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<PaymentProcessedConsumer>();
                x.AddConsumer<InventoryFailedConsumer>();
            })
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new PaymentProcessedEvent(created.Id, Guid.NewGuid(), created.TotalAmount, DateTime.UtcNow));
        Assert.True(await harness.Consumed.Any<PaymentProcessedEvent>());

        await harness.Bus.Publish(new InventoryFailedEvent(created.Id, "insufficient stock for product X", DateTime.UtcNow));
        Assert.True(await harness.Consumed.Any<InventoryFailedEvent>());

        Assert.Equal(OrderStatus.InventoryFailed, (await repository.GetByIdAsync(created.Id, default))!.Status);
        publisher.Verify(p => p.PublishOrderCompletedAsync(It.IsAny<Guid>(), It.IsAny<OrderStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DuplicatePaymentProcessedEvent_AfterAlreadyAdvanced_DoesNotPublishStatusChangedAgain()
    {
        var (manager, repository, publisher) = CreateManager();
        var created = await manager.CreateOrderAsync(ValidRequest(), null, default);

        await using var provider = new ServiceCollection()
            .AddSingleton(manager)
            .AddMassTransitTestHarness(x => x.AddConsumer<PaymentProcessedConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var @event = new PaymentProcessedEvent(created.Id, Guid.NewGuid(), created.TotalAmount, DateTime.UtcNow);
        await harness.Bus.Publish(@event);
        Assert.True(await harness.Consumed.Any<PaymentProcessedEvent>());

        // Redelivery of the same event (MassTransit is at-least-once) must not
        // move the order past InventoryProcessing nor re-publish the transition.
        // The in-memory transport processes messages essentially immediately,
        // so a short wait is enough to let the second consumption complete.
        await harness.Bus.Publish(@event);
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Assert.Equal(OrderStatus.InventoryProcessing, (await repository.GetByIdAsync(created.Id, default))!.Status);
        publisher.Verify(p => p.PublishOrderStatusChangedAsync(
            created.Id, OrderStatus.PaymentProcessing, OrderStatus.InventoryProcessing, It.IsAny<CancellationToken>()), Times.Once);
    }
}
