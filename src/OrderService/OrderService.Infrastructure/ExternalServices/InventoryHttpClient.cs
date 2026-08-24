using System.Net;
using System.Net.Http.Json;
using OrderService.Application.Abstractions;
using OrderService.Application.Exceptions;

namespace OrderService.Infrastructure.ExternalServices;

internal sealed record InventoryProductDto(Guid Id, string Sku, string Name, decimal UnitPrice, int StockQuantity, int ReservedQuantity);

/// Talks to Inventory Service's GET /api/v1/products/{id} directly - an
/// internal service-to-service call, not frontend traffic, so it is not
/// routed through the API Gateway (see CLAUDE.md, "Architecture:
/// Non-Negotiable Rules": synchronous calls are allowed via the Gateway
/// "or explicitly documented REST calls").
public class InventoryHttpClient : IInventoryAvailabilityChecker
{
    private readonly HttpClient _httpClient;

    public InventoryHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> IsStockAvailableAsync(IReadOnlyCollection<StockCheckItem> items, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync($"/api/v1/products/{item.ProductId}", cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new InventoryServiceUnavailableException("Inventory Service is unreachable.", ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InventoryServiceUnavailableException("Inventory Service did not respond in time.", ex);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
                return false;

            response.EnsureSuccessStatusCode();

            var product = await response.Content.ReadFromJsonAsync<InventoryProductDto>(cancellationToken);
            if (product is null)
                return false;

            var availableQuantity = product.StockQuantity - product.ReservedQuantity;
            if (availableQuantity < item.Quantity)
                return false;
        }

        return true;
    }
}
