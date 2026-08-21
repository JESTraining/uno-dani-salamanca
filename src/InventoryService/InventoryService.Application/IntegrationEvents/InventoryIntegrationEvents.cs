// See the comment in OrderService.Application/IntegrationEvents/OrderIntegrationEvents.cs
// for why this namespace is deliberately bare "IntegrationEvents", not
// "InventoryService.Application.IntegrationEvents".
namespace IntegrationEvents;

/// Consumed events. Wire contracts fixed in CLAUDE.md ("Architecture:
/// Non-Negotiable Rules"), owned by Order Service and Payment Service
/// respectively. Kept as local copies by design (see CLAUDE.md,
/// "Established Architecture Pattern").
public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    IReadOnlyCollection<OrderCreatedEventItem> Items,
    DateTime Timestamp);

public sealed record OrderCreatedEventItem(Guid ProductId, int Quantity);

public sealed record PaymentProcessedEvent(Guid OrderId, Guid TransactionId, decimal Amount, DateTime Timestamp);

/// Published events. Wire contract fixed in CLAUDE.md. Any field change
/// here must be mirrored there in the same change.
public sealed record InventoryReservedEvent(Guid OrderId, IReadOnlyCollection<InventoryReservedEventItem> Items, DateTime Timestamp);

public sealed record InventoryReservedEventItem(Guid ProductId, int Quantity);

public sealed record InventoryFailedEvent(Guid OrderId, string Reason, DateTime Timestamp);
