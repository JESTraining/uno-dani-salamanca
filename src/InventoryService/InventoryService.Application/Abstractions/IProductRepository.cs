using InventoryService.Domain;

namespace InventoryService.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Product> Items, int TotalCount)> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);
}
