using InventoryService.Application.Contracts;

namespace InventoryService.Application.Abstractions;

public interface IProductManager
{
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);

    Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ProductResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<ProductResponse> UpdateStockAsync(Guid id, int newStockQuantity, CancellationToken cancellationToken);
}
