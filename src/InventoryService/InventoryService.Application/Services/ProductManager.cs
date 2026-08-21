using InventoryService.Application.Abstractions;
using InventoryService.Application.Contracts;
using InventoryService.Application.Exceptions;
using InventoryService.Domain;

namespace InventoryService.Application.Services;

public class ProductManager : IProductManager
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductManager(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = Product.Create(request.Sku, request.Name, request.Description, request.UnitPrice, request.StockQuantity);

        await _productRepository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(product);
    }

    public async Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        return product is null ? null : MapToResponse(product);
    }

    public async Task<PagedResult<ProductResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _productRepository.ListAsync(page, pageSize, cancellationToken);
        return new PagedResult<ProductResponse>(items.Select(MapToResponse).ToList(), page, pageSize, totalCount);
    }

    public async Task<ProductResponse> UpdateStockAsync(Guid id, int newStockQuantity, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ProductNotFoundException(id);

        product.UpdateStock(newStockQuantity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(product);
    }

    private static ProductResponse MapToResponse(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Description,
        product.UnitPrice,
        product.StockQuantity,
        product.ReservedQuantity,
        product.AvailableQuantity,
        product.CreatedAt,
        product.UpdatedAt);
}
