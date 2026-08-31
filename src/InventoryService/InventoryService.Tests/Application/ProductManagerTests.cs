using InventoryService.Application.Abstractions;
using InventoryService.Application.Contracts;
using InventoryService.Application.Exceptions;
using InventoryService.Application.Services;
using InventoryService.Domain;
using Moq;

namespace InventoryService.Tests.Application;

public class ProductManagerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ProductManager _sut;

    public ProductManagerTests()
    {
        _sut = new ProductManager(_productRepository.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task CreateAsync_PersistsAndReturnsProduct()
    {
        var request = new CreateProductRequest("SKU-1", "Cat Tower", "A tall one", 79.99m, 20);

        var response = await _sut.CreateAsync(request, default);

        Assert.Equal("SKU-1", response.Sku);
        Assert.Equal(20, response.AvailableQuantity);
        _productRepository.Verify(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        var response = await _sut.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(response);
    }

    [Fact]
    public async Task UpdateStockAsync_WhenProductNotFound_Throws()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        await Assert.ThrowsAsync<ProductNotFoundException>(() => _sut.UpdateStockAsync(Guid.NewGuid(), 50, default));
    }

    [Fact]
    public async Task UpdateStockAsync_WhenFound_UpdatesQuantity()
    {
        var product = Product.Create("SKU-1", "Cat Tower", null, 79.99m, 20);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var response = await _sut.UpdateStockAsync(product.Id, 100, default);

        Assert.Equal(100, response.StockQuantity);
    }
}
