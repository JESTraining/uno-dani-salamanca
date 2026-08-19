namespace OrderService.Application.Abstractions;

public sealed record StockCheckItem(Guid ProductId, int Quantity);

/// Port towards the Inventory Service. Implemented over HTTP in Infrastructure;
/// the real Inventory Service does not exist until Phase 2, so calls will fail
/// until then. Order creation must not succeed without a confirmed stock check.
public interface IInventoryAvailabilityChecker
{
    Task<bool> IsStockAvailableAsync(IReadOnlyCollection<StockCheckItem> items, CancellationToken cancellationToken);
}
