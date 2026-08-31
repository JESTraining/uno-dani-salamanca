using InventoryService.Application.Abstractions;
using InventoryService.Application.Exceptions;
using InventoryService.Domain;

namespace InventoryService.Application.Services;

public class ReservationManager : IReservationManager
{
    private const int MaxConcurrencyRetries = 3;
    private static readonly TimeSpan ReservationWindow = TimeSpan.FromMinutes(5);

    private readonly IProductRepository _productRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IOrderItemSnapshotStore _snapshotStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryEventPublisher _eventPublisher;

    public ReservationManager(
        IProductRepository productRepository,
        IReservationRepository reservationRepository,
        IOrderItemSnapshotStore snapshotStore,
        IUnitOfWork unitOfWork,
        IInventoryEventPublisher eventPublisher)
    {
        _productRepository = productRepository;
        _reservationRepository = reservationRepository;
        _snapshotStore = snapshotStore;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
    }

    public Task RecordOrderItemsAsync(Guid orderId, IReadOnlyCollection<OrderItemSnapshotEntry> items, CancellationToken cancellationToken) =>
        _snapshotStore.SaveAsync(orderId, items, cancellationToken);

    public async Task ReserveForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (await _reservationRepository.AnyForOrderAsync(orderId, cancellationToken))
            return;

        var items = await _snapshotStore.GetAsync(orderId, cancellationToken);
        if (items.Count == 0)
        {
            await _eventPublisher.PublishInventoryFailedAsync(orderId, "No order items were recorded for this order.", cancellationToken);
            return;
        }

        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            try
            {
                foreach (var item in items)
                {
                    var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken)
                        ?? throw new InvalidInventoryOperationException($"Product '{item.ProductId}' does not exist.");

                    product.Reserve(item.Quantity);

                    var reservation = InventoryReservation.Create(orderId, item.ProductId, item.Quantity, ReservationWindow);
                    await _reservationRepository.AddAsync(reservation, cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _eventPublisher.PublishInventoryReservedAsync(orderId, items, cancellationToken);
                return;
            }
            catch (InvalidInventoryOperationException ex)
            {
                await _eventPublisher.PublishInventoryFailedAsync(orderId, ex.Message, cancellationToken);
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < MaxConcurrencyRetries)
            {
                // Another reservation touched the same product concurrently; retry with fresh data.
            }
        }

        await _eventPublisher.PublishInventoryFailedAsync(orderId, "Could not reserve stock due to concurrent updates. Please retry.", cancellationToken);
    }

    public async Task<int> ReleaseExpiredReservationsAsync(CancellationToken cancellationToken)
    {
        var expired = await _reservationRepository.GetExpiredAsync(DateTime.UtcNow, cancellationToken);
        if (expired.Count == 0)
            return 0;

        foreach (var reservation in expired)
        {
            var product = await _productRepository.GetByIdAsync(reservation.ProductId, cancellationToken);
            product?.ReleaseReservation(reservation.Quantity);
            reservation.Expire();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var reservation in expired)
            await _eventPublisher.PublishInventoryFailedAsync(reservation.OrderId, "Inventory reservation expired after 5 minutes without confirmation.", cancellationToken);

        return expired.Count;
    }
}
