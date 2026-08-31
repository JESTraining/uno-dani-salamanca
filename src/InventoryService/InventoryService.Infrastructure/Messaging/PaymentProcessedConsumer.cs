using InventoryService.Application.Abstractions;
using IntegrationEvents;
using MassTransit;

namespace InventoryService.Infrastructure.Messaging;

/// Drives the saga's reservation step. Idempotent by construction -
/// IReservationManager checks for an existing reservation for the order
/// before reserving, so redelivery of this event does not double-reserve.
public class PaymentProcessedConsumer : IConsumer<PaymentProcessedEvent>
{
    private readonly IReservationManager _reservationManager;

    public PaymentProcessedConsumer(IReservationManager reservationManager)
    {
        _reservationManager = reservationManager;
    }

    public Task Consume(ConsumeContext<PaymentProcessedEvent> context) =>
        _reservationManager.ReserveForOrderAsync(context.Message.OrderId, context.CancellationToken);
}
