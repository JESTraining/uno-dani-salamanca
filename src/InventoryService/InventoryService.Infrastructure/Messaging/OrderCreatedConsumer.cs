using InventoryService.Application.Abstractions;
using IntegrationEvents;
using MassTransit;

namespace InventoryService.Infrastructure.Messaging;

/// Only records what each order contains (see IOrderItemSnapshotStore) - it
/// does not reserve anything yet. Reservation happens on PaymentProcessedEvent
/// (see PaymentProcessedConsumer), per the saga flow in the original exercise.
public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly IReservationManager _reservationManager;

    public OrderCreatedConsumer(IReservationManager reservationManager)
    {
        _reservationManager = reservationManager;
    }

    public Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var items = context.Message.Items.Select(i => new OrderItemSnapshotEntry(i.ProductId, i.Quantity)).ToList();
        return _reservationManager.RecordOrderItemsAsync(context.Message.OrderId, items, context.CancellationToken);
    }
}
