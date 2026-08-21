using InventoryService.Application.Abstractions;
using InventoryService.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryService.Tests.Integration;

/// Exercises the actual overselling-prevention requirement against a real
/// database: two orders racing for the last unit of stock. Each reservation
/// attempt runs in its own DI scope (own DbContext/connection), matching
/// how two concurrent HTTP or consumer calls would behave in production.
public class ConcurrentReservationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ConcurrentReservationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.CreateClient(); // forces the host (and its DI container) to build
    }

    [Fact]
    public async Task ReserveForOrderAsync_TwoOrdersRacingForTheLastUnit_ExactlyOneSucceeds()
    {
        Guid productId;
        using (var scope = _factory.Services.CreateScope())
        {
            var productManager = scope.ServiceProvider.GetRequiredService<IProductManager>();
            var product = await productManager.CreateAsync(
                new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Limited Cat Gym", null, 199.99m, StockQuantity: 1),
                default);
            productId = product.Id;
        }

        var orderId1 = Guid.NewGuid();
        var orderId2 = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var reservationManager = scope.ServiceProvider.GetRequiredService<IReservationManager>();
            await reservationManager.RecordOrderItemsAsync(orderId1, new[] { new OrderItemSnapshotEntry(productId, 1) }, default);
            await reservationManager.RecordOrderItemsAsync(orderId2, new[] { new OrderItemSnapshotEntry(productId, 1) }, default);
        }

        var task1 = ReserveInOwnScopeAsync(orderId1);
        var task2 = ReserveInOwnScopeAsync(orderId2);
        await Task.WhenAll(task1, task2);

        using var assertScope = _factory.Services.CreateScope();
        var finalProductManager = assertScope.ServiceProvider.GetRequiredService<IProductManager>();
        var finalProduct = await finalProductManager.GetByIdAsync(productId, default);

        Assert.True(finalProduct!.ReservedQuantity <= finalProduct.StockQuantity, "Reserved quantity must never exceed stock.");
        Assert.Equal(1, finalProduct.ReservedQuantity);
    }

    private Task ReserveInOwnScopeAsync(Guid orderId) => Task.Run(async () =>
    {
        using var scope = _factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IReservationManager>();
        await manager.ReserveForOrderAsync(orderId, default);
    });
}
