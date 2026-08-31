using OrderService.Domain;

namespace OrderService.Application.Abstractions;

public interface IOrderEventPublisher
{
    Task PublishOrderCreatedAsync(Order order, CancellationToken cancellationToken);

    Task PublishOrderStatusChangedAsync(
        Guid orderId,
        OrderStatus previousStatus,
        OrderStatus newStatus,
        CancellationToken cancellationToken);

    Task PublishOrderCompletedAsync(Guid orderId, OrderStatus status, CancellationToken cancellationToken);
}
