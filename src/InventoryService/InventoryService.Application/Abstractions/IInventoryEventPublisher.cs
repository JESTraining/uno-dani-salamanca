namespace InventoryService.Application.Abstractions;

public interface IInventoryEventPublisher
{
    Task PublishInventoryReservedAsync(Guid orderId, IReadOnlyCollection<OrderItemSnapshotEntry> items, CancellationToken cancellationToken);

    Task PublishInventoryFailedAsync(Guid orderId, string reason, CancellationToken cancellationToken);
}
