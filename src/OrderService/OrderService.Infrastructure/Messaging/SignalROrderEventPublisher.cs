using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OrderService.Application.Abstractions;
using OrderService.Domain;

namespace OrderService.Infrastructure.Messaging;

public class SignalROrderEventPublisher : IOrderEventPublisher
{
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<SignalROrderEventPublisher> _logger;

    public SignalROrderEventPublisher(IHubContext<OrderHub> hubContext, ILogger<SignalROrderEventPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task PublishOrderCreatedAsync(Order order, CancellationToken cancellationToken) =>
        BroadcastAsync(
            "OrderCreated",
            new OrderCreatedNotification(order.Id, order.CustomerId, order.CustomerName, order.Status.ToString(), order.TotalAmount, order.CreatedAt),
            cancellationToken);

    public Task PublishOrderStatusChangedAsync(
        Guid orderId,
        OrderStatus previousStatus,
        OrderStatus newStatus,
        CancellationToken cancellationToken) =>
        BroadcastAsync(
            "OrderStatusChanged",
            new OrderStatusChangedNotification(orderId, previousStatus.ToString(), newStatus.ToString(), DateTime.UtcNow),
            cancellationToken);

    public Task PublishOrderCompletedAsync(Guid orderId, OrderStatus status, CancellationToken cancellationToken) =>
        BroadcastAsync(
            "OrderCompleted",
            new OrderCompletedNotification(orderId, status.ToString(), DateTime.UtcNow),
            cancellationToken);

    /// A broadcast failure is a best-effort UI concern - it must never fail
    /// the order operation that triggered it, unlike a lost RabbitMQ event.
    private async Task BroadcastAsync<T>(string method, T payload, CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(method, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast {Method} over SignalR.", method);
        }
    }
}
