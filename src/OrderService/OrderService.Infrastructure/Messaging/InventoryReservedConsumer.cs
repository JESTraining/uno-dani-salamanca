using IntegrationEvents;
using MassTransit;
using OrderService.Application.Abstractions;

namespace OrderService.Infrastructure.Messaging;

/// Drives the saga's final happy-path step: the order lands in the terminal
/// Completed status and OrderCompletedEvent is published. Idempotent by
/// construction - IOrderManager no-ops if the order already completed.
public class InventoryReservedConsumer : IConsumer<InventoryReservedEvent>
{
    private readonly IOrderManager _orderManager;

    public InventoryReservedConsumer(IOrderManager orderManager)
    {
        _orderManager = orderManager;
    }

    public Task Consume(ConsumeContext<InventoryReservedEvent> context) =>
        _orderManager.CompleteOrderAsync(context.Message.OrderId, context.CancellationToken);
}
