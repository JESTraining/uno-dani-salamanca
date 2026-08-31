using IntegrationEvents;
using MassTransit;
using PaymentService.Application.Abstractions;

namespace PaymentService.Infrastructure.Messaging;

/// Drives the saga's payment step: every OrderCreatedEvent triggers a
/// payment attempt. Idempotent by construction - IPaymentManager checks
/// for an existing payment for the order before processing, so redelivery
/// of this event does not charge twice.
public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly IPaymentManager _paymentManager;

    public OrderCreatedConsumer(IPaymentManager paymentManager)
    {
        _paymentManager = paymentManager;
    }

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var message = context.Message;
        await _paymentManager.ProcessPaymentAsync(message.OrderId, message.TotalAmount, paymentMethod: null, context.CancellationToken);
    }
}
