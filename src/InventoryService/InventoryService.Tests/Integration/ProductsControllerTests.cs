using System.Net;
using System.Net.Http.Json;
using InventoryService.Application.Contracts;

namespace InventoryService.Tests.Integration;

public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreatedWithAvailableQuantity()
    {
        var request = new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Cat Tower", "A tall one", 79.99m, 25);

        var response = await _client.PostAsJsonAsync("/api/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal(25, product!.AvailableQuantity);
    }

    [Fact]
    public async Task GetById_AfterCreate_ReturnsTheProduct()
    {
        var created = await (await _client.PostAsJsonAsync("/api/products", new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Dog Mat", null, 29.99m, 10)))
            .Content.ReadFromJsonAsync<ProductResponse>();

        var response = await _client.GetAsync($"/api/products/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStock_ChangesStockQuantity()
    {
        var created = await (await _client.PostAsJsonAsync("/api/products", new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Cat Gym", null, 129.99m, 5)))
            .Content.ReadFromJsonAsync<ProductResponse>();

        var response = await _client.PutAsJsonAsync($"/api/products/{created!.Id}/stock", new UpdateStockRequest(50));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal(50, updated!.StockQuantity);
    }

    [Fact]
    public async Task UpdateStock_WhenProductNotFound_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync($"/api/products/{Guid.NewGuid()}/stock", new UpdateStockRequest(50));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsPagedResult()
    {
        await _client.PostAsJsonAsync("/api/products", new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Scratching Post", null, 39.99m, 15));

        var response = await _client.GetAsync("/api/products?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductResponse>>();
        Assert.True(page!.TotalCount >= 1);
    }
}
