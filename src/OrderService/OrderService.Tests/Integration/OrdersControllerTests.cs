using System.Net;
using System.Net.Http.Json;
using OrderService.Application.Contracts;

namespace OrderService.Tests.Integration;

public class OrdersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrdersControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static CreateOrderRequest ValidRequest() => new(
        Guid.NewGuid(), "Jane Doe", "jane@example.com",
        new[] { new CreateOrderItemRequest(Guid.NewGuid(), "Widget", 2, 9.99m) });

    [Fact]
    public async Task CreateOrder_ThenGetById_ReturnsTheSameOrder()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/orders", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var getResponse = await _client.GetAsync($"/api/orders/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(created.Id, fetched!.Id);
        // Saga kickoff: creation synchronously advances Pending -> PaymentProcessing.
        Assert.Equal("PaymentProcessing", fetched.Status);
        Assert.Single(fetched.Items);
    }

    [Fact]
    public async Task CreateOrder_WithRepeatedIdempotencyKey_DoesNotCreateADuplicate()
    {
        var key = Guid.NewGuid().ToString();
        var request = ValidRequest();

        var first = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(request) };
        first.Headers.Add("Idempotency-Key", key);
        var firstResponse = await _client.SendAsync(first);
        var firstOrder = await firstResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var second = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(request) };
        second.Headers.Add("Idempotency-Key", key);
        var secondResponse = await _client.SendAsync(second);
        var secondOrder = await secondResponse.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(firstOrder!.Id, secondOrder!.Id);
    }

    [Fact]
    public async Task CreateOrder_WithNoItems_ReturnsBadRequest()
    {
        var invalidRequest = new CreateOrderRequest(Guid.NewGuid(), "Jane Doe", "jane@example.com", Array.Empty<CreateOrderItemRequest>());

        var response = await _client.PostAsJsonAsync("/api/orders", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WithInvalidStatusValue_ReturnsBadRequest()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/orders", ValidRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var response = await _client.PutAsJsonAsync($"/api/orders/{created!.Id}/status", new UpdateOrderStatusRequest("NotARealStatus"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrder_WhilePaymentProcessing_CancelsIt()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/orders", ValidRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var cancelResponse = await _client.DeleteAsync($"/api/orders/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.Equal("Cancelled", cancelled!.Status);
    }

    [Fact]
    public async Task DeleteOrder_AfterShipped_ReturnsConflict()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/orders", ValidRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var shipResponse = await _client.PutAsJsonAsync($"/api/orders/{created!.Id}/status", new UpdateOrderStatusRequest("Shipped"));
        Assert.Equal(HttpStatusCode.OK, shipResponse.StatusCode);

        var cancelResponse = await _client.DeleteAsync($"/api/orders/{created.Id}");

        Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task GetById_WhenOrderDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsCreatedOrderFilteredByCustomer()
    {
        var request = ValidRequest();
        var createResponse = await _client.PostAsJsonAsync("/api/orders", request);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var listResponse = await _client.GetAsync($"/api/orders?page=1&pageSize=10&customerId={request.CustomerId}");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<OrderResponse>>();

        Assert.Equal(1, page!.TotalCount);
        Assert.Equal(created!.Id, page.Items.Single().Id);
    }

    [Fact]
    public async Task List_FilteredByStatusPending_ExcludesCancelledOrders()
    {
        var cancelledOrderResponse = await _client.PostAsJsonAsync("/api/orders", ValidRequest());
        var cancelledOrder = await cancelledOrderResponse.Content.ReadFromJsonAsync<OrderResponse>();
        await _client.DeleteAsync($"/api/orders/{cancelledOrder!.Id}");

        var listResponse = await _client.GetAsync("/api/orders?status=Cancelled&pageSize=50");
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<OrderResponse>>();

        Assert.Contains(page!.Items, o => o.Id == cancelledOrder.Id);
        Assert.All(page.Items, o => Assert.Equal("Cancelled", o.Status));
    }
}
