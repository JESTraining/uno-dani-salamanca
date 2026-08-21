using IntegrationEvents;
using MassTransit;
using OrderService.Application.Abstractions;
using OrderService.Domain;

namespace OrderService.Infrastructure.Messaging;

public class RabbitMqOrderEventPublisher : IOrderEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public RabbitMqOrderEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishOrderCreatedAsync(Order order, CancellationToken cancellationToken)
    {
        var @event = new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Items.Select(i => new OrderCreatedEventItem(i.ProductId, i.Quantity)).ToList(),
            DateTime.UtcNow);

        return _publishEndpoint.Publish(@event, cancellationToken);
    }

    public Task PublishOrderStatusChangedAsync(
        Guid orderId,
        OrderStatus previousStatus,
        OrderStatus newStatus,
        CancellationToken cancellationToken)
    {
        var @event = new OrderStatusChangedEvent(
            orderId,
            previousStatus.ToString(),
            newStatus.ToString(),
            DateTime.UtcNow);

        return _publishEndpoint.Publish(@event, cancellationToken);
    }
}
