namespace OrderService.Application.Abstractions;

public interface IIdempotencyStore
{
    Task<Guid?> FindExistingOrderIdAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// Stages the key for the same unit of work that persists the order, so both
    /// commit atomically together (prevents duplicate orders on concurrent retries).
    Task SaveAsync(string idempotencyKey, Guid orderId, CancellationToken cancellationToken);
}
