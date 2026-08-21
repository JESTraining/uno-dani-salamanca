namespace InventoryService.Application.Abstractions;

public interface IReservationManager
{
    Task RecordOrderItemsAsync(Guid orderId, IReadOnlyCollection<OrderItemSnapshotEntry> items, CancellationToken cancellationToken);

    Task ReserveForOrderAsync(Guid orderId, CancellationToken cancellationToken);

    /// Releases reservations past their 5-minute expiration window. Called
    /// by the periodic background service (see CLAUDE.md, "Key Design
    /// Decisions" - resolved with the user to ship this now, not deferred).
    Task<int> ReleaseExpiredReservationsAsync(CancellationToken cancellationToken);
}
