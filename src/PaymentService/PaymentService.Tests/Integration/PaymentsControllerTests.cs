using System.Net;
using System.Net.Http.Json;
using PaymentService.Application.Contracts;

namespace PaymentService.Tests.Integration;

public class PaymentsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PaymentsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProcessPayment_RegularAmount_ReturnsSucceeded()
    {
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 150m, "CreditCard");

        var response = await _client.PostAsJsonAsync("/api/payments/process", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.Equal("Succeeded", payment!.Status);
        Assert.NotNull(payment.TransactionId);
    }

    [Fact]
    public async Task ProcessPayment_AmountOverTenThousand_ReturnsFailedOutcomeWithHttp200()
    {
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 10000.01m, "CreditCard");

        var response = await _client.PostAsJsonAsync("/api/payments/process", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.Equal("Failed", payment!.Status);
        Assert.Null(payment.TransactionId);
    }

    [Fact]
    public async Task ProcessPayment_SameOrderTwice_ReturnsTheSamePaymentBothTimes()
    {
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 150m, "CreditCard");

        var first = await (await _client.PostAsJsonAsync("/api/payments/process", request)).Content.ReadFromJsonAsync<PaymentResponse>();
        var second = await (await _client.PostAsJsonAsync("/api/payments/process", request)).Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task GetByOrderId_WhenNoPaymentProcessed_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByOrderId_AfterProcessing_ReturnsThePayment()
    {
        var orderId = Guid.NewGuid();
        await _client.PostAsJsonAsync("/api/payments/process", new ProcessPaymentRequest(orderId, 150m, "CreditCard"));

        var response = await _client.GetAsync($"/api/payments/{orderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.Equal(orderId, payment!.OrderId);
    }
}
