using InventoryService.Application.Abstractions;
using IntegrationEvents;
using MassTransit;

namespace InventoryService.Infrastructure.Messaging;

public class RabbitMqInventoryEventPublisher : IInventoryEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public RabbitMqInventoryEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishInventoryReservedAsync(Guid orderId, IReadOnlyCollection<OrderItemSnapshotEntry> items, CancellationToken cancellationToken)
    {
        var eventItems = items.Select(i => new InventoryReservedEventItem(i.ProductId, i.Quantity)).ToList();
        var @event = new InventoryReservedEvent(orderId, eventItems, DateTime.UtcNow);
        return _publishEndpoint.Publish(@event, cancellationToken);
    }

    public Task PublishInventoryFailedAsync(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        var @event = new InventoryFailedEvent(orderId, reason, DateTime.UtcNow);
        return _publishEndpoint.Publish(@event, cancellationToken);
    }
}
