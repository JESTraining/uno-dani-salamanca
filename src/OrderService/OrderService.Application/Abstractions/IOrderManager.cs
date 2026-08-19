using OrderService.Application.Contracts;
using OrderService.Domain;

namespace OrderService.Application.Abstractions;

public interface IOrderManager
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    Task<OrderResponse?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<OrderResponse>> ListOrdersAsync(OrderListQuery query, CancellationToken cancellationToken);

    Task<OrderResponse> UpdateStatusAsync(Guid id, OrderStatus newStatus, CancellationToken cancellationToken);

    Task<OrderResponse> CancelOrderAsync(Guid id, CancellationToken cancellationToken);
}
