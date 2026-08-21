using InventoryService.Application.Abstractions;
using InventoryService.Application.Exceptions;
using InventoryService.Application.Services;
using InventoryService.Domain;
using Moq;

namespace InventoryService.Tests.Application;

public class ReservationManagerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IReservationRepository> _reservationRepository = new();
    private readonly Mock<IOrderItemSnapshotStore> _snapshotStore = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IInventoryEventPublisher> _eventPublisher = new();
    private readonly ReservationManager _sut;

    public ReservationManagerTests()
    {
        _sut = new ReservationManager(
            _productRepository.Object,
            _reservationRepository.Object,
            _snapshotStore.Object,
            _unitOfWork.Object,
            _eventPublisher.Object);
    }

    private static Product ProductWithStock(int stock) => Product.Create("SKU-1", "Cat Tower", null, 49.99m, stock);

    [Fact]
    public async Task ReserveForOrderAsync_WhenAlreadyReservedForOrder_DoesNothing()
    {
        var orderId = Guid.NewGuid();
        _reservationRepository.Setup(r => r.AnyForOrderAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _sut.ReserveForOrderAsync(orderId, default);

        _snapshotStore.Verify(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisher.Verify(p => p.PublishInventoryReservedAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<OrderItemSnapshotEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReserveForOrderAsync_WithNoRecordedItems_PublishesFailed()
    {
        var orderId = Guid.NewGuid();
        _reservationRepository.Setup(r => r.AnyForOrderAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _snapshotStore.Setup(s => s.GetAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<OrderItemSnapshotEntry>());

        await _sut.ReserveForOrderAsync(orderId, default);

        _eventPublisher.Verify(p => p.PublishInventoryFailedAsync(orderId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveForOrderAsync_HappyPath_ReservesAndPublishesReserved()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var product = ProductWithStock(10);

        _reservationRepository.Setup(r => r.AnyForOrderAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _snapshotStore.Setup(s => s.GetAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderItemSnapshotEntry> { new(productId, 3) });
        _productRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        await _sut.ReserveForOrderAsync(orderId, default);

        Assert.Equal(3, product.ReservedQuantity);
        _reservationRepository.Verify(r => r.AddAsync(It.IsAny<InventoryReservation>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisher.Verify(p => p.PublishInventoryReservedAsync(orderId, It.IsAny<IReadOnlyCollection<OrderItemSnapshotEntry>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveForOrderAsync_WhenStockInsufficient_PublishesFailedAndDoesNotThrow()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var product = ProductWithStock(1);

        _reservationRepository.Setup(r => r.AnyForOrderAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _snapshotStore.Setup(s => s.GetAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderItemSnapshotEntry> { new(productId, 5) });
        _productRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        await _sut.ReserveForOrderAsync(orderId, default);

        _eventPublisher.Verify(p => p.PublishInventoryFailedAsync(orderId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReserveForOrderAsync_WhenConcurrencyConflictOnce_RetriesAndSucceeds()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var product = ProductWithStock(10);

        _reservationRepository.Setup(r => r.AnyForOrderAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _snapshotStore.Setup(s => s.GetAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderItemSnapshotEntry> { new(productId, 2) });
        _productRepository.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var callCount = 0;
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new ConcurrencyConflictException("conflict");
                return Task.FromResult(1);
            });

        await _sut.ReserveForOrderAsync(orderId, default);

        Assert.Equal(2, callCount);
        _eventPublisher.Verify(p => p.PublishInventoryReservedAsync(orderId, It.IsAny<IReadOnlyCollection<OrderItemSnapshotEntry>>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisher.Verify(p => p.PublishInventoryFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseExpiredReservationsAsync_ReleasesStockAndPublishesFailedPerReservation()
    {
        var product = ProductWithStock(10);
        product.Reserve(4);
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), 4, TimeSpan.FromMinutes(-1));

        _reservationRepository.Setup(r => r.GetExpiredAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventoryReservation> { reservation });
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var releasedCount = await _sut.ReleaseExpiredReservationsAsync(default);

        Assert.Equal(1, releasedCount);
        Assert.Equal(0, product.ReservedQuantity);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        _eventPublisher.Verify(p => p.PublishInventoryFailedAsync(reservation.OrderId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
