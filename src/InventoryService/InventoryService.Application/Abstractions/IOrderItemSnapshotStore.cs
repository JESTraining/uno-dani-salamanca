namespace InventoryService.Application.Abstractions;

public sealed record OrderItemSnapshotEntry(Guid ProductId, int Quantity);

/// PaymentProcessedEvent (fixed contract in CLAUDE.md) carries only OrderId,
/// TransactionId, Amount and Timestamp - no line items. Inventory Service
/// separately consumes OrderCreatedEvent to record what each order contains,
/// then looks it up here when PaymentProcessedEvent arrives and it is time
/// to actually reserve stock. This is a local read-model built from events,
/// not a query into another service's database.
public interface IOrderItemSnapshotStore
{
    Task SaveAsync(Guid orderId, IReadOnlyCollection<OrderItemSnapshotEntry> items, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderItemSnapshotEntry>> GetAsync(Guid orderId, CancellationToken cancellationToken);
}
