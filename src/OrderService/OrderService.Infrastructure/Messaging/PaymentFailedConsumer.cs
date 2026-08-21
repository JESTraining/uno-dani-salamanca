using IntegrationEvents;
using MassTransit;
using OrderService.Application.Abstractions;

namespace OrderService.Infrastructure.Messaging;

/// Drives the saga's compensation for a failed payment: the order lands in
/// the terminal PaymentFailed status. Idempotent by construction -
/// IOrderManager no-ops if the order already reached PaymentFailed.
public class PaymentFailedConsumer : IConsumer<PaymentFailedEvent>
{
    private readonly IOrderManager _orderManager;

    public PaymentFailedConsumer(IOrderManager orderManager)
    {
        _orderManager = orderManager;
    }

    public Task Consume(ConsumeContext<PaymentFailedEvent> context) =>
        _orderManager.MarkPaymentFailedAsync(context.Message.OrderId, context.Message.Reason, context.CancellationToken);
}
