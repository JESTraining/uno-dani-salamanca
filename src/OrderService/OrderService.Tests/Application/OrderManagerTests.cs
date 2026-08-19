using Moq;
using OrderService.Application.Abstractions;
using OrderService.Application.Contracts;
using OrderService.Application.Exceptions;
using OrderService.Application.Services;
using OrderService.Domain;

namespace OrderService.Tests.Application;

public class OrderManagerTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IInventoryAvailabilityChecker> _inventoryChecker = new();
    private readonly Mock<IOrderEventPublisher> _eventPublisher = new();
    private readonly Mock<IIdempotencyStore> _idempotencyStore = new();
    private readonly OrderManager _sut;

    public OrderManagerTests()
    {
        _sut = new OrderManager(
            _orderRepository.Object,
            _unitOfWork.Object,
            _inventoryChecker.Object,
            _eventPublisher.Object,
            _idempotencyStore.Object);
    }

    private static CreateOrderRequest ValidRequest() => new(
        Guid.NewGuid(), "Jane Doe", "jane@example.com",
        new[] { new CreateOrderItemRequest(Guid.NewGuid(), "Widget", 2, 9.99m) });

    [Fact]
    public async Task CreateOrderAsync_WhenStockUnavailable_ThrowsAndDoesNotPersist()
    {
        _inventoryChecker.Setup(c => c.IsStockAvailableAsync(It.IsAny<IReadOnlyCollection<StockCheckItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InsufficientStockException>(() => _sut.CreateOrderAsync(ValidRequest(), null, default));

        _orderRepository.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisher.Verify(p => p.PublishOrderCreatedAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_HappyPath_PersistsAndPublishesCreatedEvent()
    {
        _inventoryChecker.Setup(c => c.IsStockAvailableAsync(It.IsAny<IReadOnlyCollection<StockCheckItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _sut.CreateOrderAsync(ValidRequest(), null, default);

        Assert.Equal("Pending", response.Status);
        _orderRepository.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisher.Verify(p => p.PublishOrderCreatedAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_WithReusedIdempotencyKey_ReturnsExistingOrderWithoutCreatingNew()
    {
        var existingOrder = Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com",
            new[] { new OrderItem(Guid.NewGuid(), "Widget", 1, 9.99m) });

        _idempotencyStore.Setup(s => s.FindExistingOrderIdAsync("key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder.Id);
        _orderRepository.Setup(r => r.GetByIdAsync(existingOrder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        var response = await _sut.CreateOrderAsync(ValidRequest(), "key-1", default);

        Assert.Equal(existingOrder.Id, response.Id);
        _inventoryChecker.Verify(c => c.IsStockAvailableAsync(It.IsAny<IReadOnlyCollection<StockCheckItem>>(), It.IsAny<CancellationToken>()), Times.Never);
        _orderRepository.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenOrderDoesNotExist_ThrowsOrderNotFoundException()
    {
        _orderRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<OrderNotFoundException>(() => _sut.UpdateStatusAsync(Guid.NewGuid(), OrderStatus.PaymentProcessing, default));
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderNotAllowedToBeCancelled_PropagatesDomainException()
    {
        var order = Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com",
            new[] { new OrderItem(Guid.NewGuid(), "Widget", 1, 9.99m) });
        order.ChangeStatus(OrderStatus.Shipped);

        _orderRepository.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        await Assert.ThrowsAsync<InvalidOrderOperationException>(() => _sut.CancelOrderAsync(order.Id, default));
        _eventPublisher.Verify(p => p.PublishOrderStatusChangedAsync(
            It.IsAny<Guid>(), It.IsAny<OrderStatus>(), It.IsAny<OrderStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenAllowed_PublishesStatusChangedEvent()
    {
        var order = Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com",
            new[] { new OrderItem(Guid.NewGuid(), "Widget", 1, 9.99m) });

        _orderRepository.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var response = await _sut.CancelOrderAsync(order.Id, default);

        Assert.Equal("Cancelled", response.Status);
        _eventPublisher.Verify(p => p.PublishOrderStatusChangedAsync(
            order.Id, OrderStatus.Pending, OrderStatus.Cancelled, It.IsAny<CancellationToken>()), Times.Once);
    }
}
