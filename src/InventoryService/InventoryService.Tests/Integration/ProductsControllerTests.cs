using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryService.Application.Contracts;

namespace InventoryService.Tests.Integration;

public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // Create/List/Detail/Stock tests below exercise product CRUD, not
        // authorization - carrying a valid Admin token by default keeps
        // every existing test focused on its own concern. Authorization
        // itself is covered explicitly at the bottom of this file.
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken("Admin"));
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreatedWithAvailableQuantity()
    {
        var request = new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Cat Tower", "A tall one", 79.99m, 25);

        var response = await _client.PostAsJsonAsync("/api/v1/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal(25, product!.AvailableQuantity);
    }

    [Fact]
    public async Task GetById_AfterCreate_ReturnsTheProduct()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Dog Mat", null, 29.99m, 10)))
            .Content.ReadFromJsonAsync<ProductResponse>();

        var response = await _client.GetAsync($"/api/v1/products/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStock_ChangesStockQuantity()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Cat Gym", null, 129.99m, 5)))
            .Content.ReadFromJsonAsync<ProductResponse>();

        var response = await _client.PutAsJsonAsync($"/api/v1/products/{created!.Id}/stock", new UpdateStockRequest(50));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal(50, updated!.StockQuantity);
    }

    [Fact]
    public async Task UpdateStock_WhenProductNotFound_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync($"/api/v1/products/{Guid.NewGuid()}/stock", new UpdateStockRequest(50));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsPagedResult()
    {
        await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Scratching Post", null, 39.99m, 15));

        var response = await _client.GetAsync("/api/v1/products?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductResponse>>();
        Assert.True(page!.TotalCount >= 1);
    }

    [Fact]
    public async Task CreateProduct_WithoutToken_ReturnsUnauthorized()
    {
        var anonymousClient = _factory.CreateClient();
        var request = new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Cat Tower", null, 79.99m, 25);

        var response = await anonymousClient.PostAsJsonAsync("/api/v1/products", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithNonAdminRole_ReturnsForbidden()
    {
        var customerClient = _factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken("Customer"));
        var request = new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Cat Tower", null, 79.99m, 25);

        var response = await customerClient.PostAsJsonAsync("/api/v1/products", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
