using IntegrationEvents;
using MassTransit;
using OrderService.Application.Abstractions;

namespace OrderService.Infrastructure.Messaging;

/// Drives the saga's compensation for a failed reservation: the order lands
/// in the terminal InventoryFailed status. Idempotent by construction -
/// IOrderManager no-ops if the order already reached InventoryFailed.
public class InventoryFailedConsumer : IConsumer<InventoryFailedEvent>
{
    private readonly IOrderManager _orderManager;

    public InventoryFailedConsumer(IOrderManager orderManager)
    {
        _orderManager = orderManager;
    }

    public Task Consume(ConsumeContext<InventoryFailedEvent> context) =>
        _orderManager.MarkInventoryFailedAsync(context.Message.OrderId, context.Message.Reason, context.CancellationToken);
}
