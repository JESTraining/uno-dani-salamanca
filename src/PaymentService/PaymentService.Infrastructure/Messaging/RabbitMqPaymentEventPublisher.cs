using IntegrationEvents;
using MassTransit;
using PaymentService.Application.Abstractions;
using PaymentService.Domain;

namespace PaymentService.Infrastructure.Messaging;

public class RabbitMqPaymentEventPublisher : IPaymentEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public RabbitMqPaymentEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishPaymentProcessedAsync(Payment payment, CancellationToken cancellationToken)
    {
        var @event = new PaymentProcessedEvent(payment.OrderId, payment.TransactionId!.Value, payment.Amount, DateTime.UtcNow);
        return _publishEndpoint.Publish(@event, cancellationToken);
    }

    public Task PublishPaymentFailedAsync(Payment payment, CancellationToken cancellationToken)
    {
        var @event = new PaymentFailedEvent(payment.OrderId, payment.FailureReason ?? "Unknown reason", DateTime.UtcNow);
        return _publishEndpoint.Publish(@event, cancellationToken);
    }
}
