// See the comment in OrderService.Application/IntegrationEvents/OrderIntegrationEvents.cs
// for why this namespace is deliberately bare "IntegrationEvents", not
// "PaymentService.Application.IntegrationEvents".
namespace IntegrationEvents;

/// Consumed event. Wire contract fixed in CLAUDE.md ("Architecture:
/// Non-Negotiable Rules") and owned by Order Service. Kept as a local copy
/// here by design (see CLAUDE.md, "Established Architecture Pattern") -
/// services do not share a contracts library.
public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    IReadOnlyCollection<OrderCreatedEventItem> Items,
    DateTime Timestamp);

public sealed record OrderCreatedEventItem(Guid ProductId, int Quantity);

/// Published events. Wire contract fixed in CLAUDE.md ("Architecture:
/// Non-Negotiable Rules"). Any field change here must be mirrored there in
/// the same change.
public sealed record PaymentProcessedEvent(Guid OrderId, Guid TransactionId, decimal Amount, DateTime Timestamp);

public sealed record PaymentFailedEvent(Guid OrderId, string Reason, DateTime Timestamp);
