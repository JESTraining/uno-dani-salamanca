namespace OrderService.Application.Abstractions;

public sealed record StockCheckItem(Guid ProductId, int Quantity);

/// Port towards Inventory Service, implemented over HTTP in Infrastructure.
/// Order creation must not succeed without a confirmed stock check.
public interface IInventoryAvailabilityChecker
{
    Task<bool> IsStockAvailableAsync(IReadOnlyCollection<StockCheckItem> items, CancellationToken cancellationToken);
}
