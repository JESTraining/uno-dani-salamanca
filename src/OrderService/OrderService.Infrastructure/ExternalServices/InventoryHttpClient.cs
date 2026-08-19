using System.Net;
using System.Net.Http.Json;
using OrderService.Application.Abstractions;
using OrderService.Application.Exceptions;

namespace OrderService.Infrastructure.ExternalServices;

internal sealed record InventoryProductDto(Guid Id, string Sku, string Name, decimal UnitPrice, int StockQuantity, int ReservedQuantity);

/// Talks to the Inventory Service's GET /api/products/{id} (see README.md,
/// "Phase 2: Payment & Inventory Services"). That service does not exist yet,
/// so every call fails until then - by design, order creation must not
/// succeed without a confirmed stock check.
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
                response = await _httpClient.GetAsync($"/api/products/{item.ProductId}", cancellationToken);
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
