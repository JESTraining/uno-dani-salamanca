using IntegrationEvents;
using MassTransit;
using OrderService.Application.Abstractions;

namespace OrderService.Infrastructure.Messaging;

/// Drives the saga's happy path from PaymentProcessing to InventoryProcessing.
/// Idempotent by construction - IOrderManager no-ops if the order already
/// advanced past PaymentProcessing, so redelivery of this event is harmless.
public class PaymentProcessedConsumer : IConsumer<PaymentProcessedEvent>
{
    private readonly IOrderManager _orderManager;

    public PaymentProcessedConsumer(IOrderManager orderManager)
    {
        _orderManager = orderManager;
    }

    public Task Consume(ConsumeContext<PaymentProcessedEvent> context) =>
        _orderManager.MarkPaymentProcessedAsync(context.Message.OrderId, context.CancellationToken);
}
