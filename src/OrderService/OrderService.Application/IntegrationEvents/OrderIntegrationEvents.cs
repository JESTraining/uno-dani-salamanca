// Deliberately NOT "OrderService.Application.IntegrationEvents": MassTransit's
// JSON envelope tags every message with a type URN built from the CLR
// namespace + type name, and a consumer only binds to messages whose URN
// matches a locally known type. Every service's local copy of a shared
// contract must use this exact same bare "IntegrationEvents" namespace (see
// CLAUDE.md, "Architecture: Non-Negotiable Rules") so the URNs line up
// across services - otherwise messages route to the right queue (per
// SetEntityName in Program.cs) but get silently dead-lettered as
// unrecognized on arrival.
namespace IntegrationEvents;

/// Wire contract fixed in CLAUDE.md ("Architecture: Non-Negotiable Rules"). Any
/// field change here must be mirrored there in the same change.
public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    IReadOnlyCollection<OrderCreatedEventItem> Items,
    DateTime Timestamp);

public sealed record OrderCreatedEventItem(Guid ProductId, int Quantity);

/// Wire contract fixed in CLAUDE.md ("Architecture: Non-Negotiable Rules").
public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    string PreviousStatus,
    string NewStatus,
    DateTime Timestamp);

/// Wire contract fixed in CLAUDE.md ("Architecture: Non-Negotiable Rules"). Published
/// by Order Service once the saga reaches its Completed terminal state.
public sealed record OrderCompletedEvent(
    Guid OrderId,
    string Status,
    DateTime Timestamp);

// Local copies of events published by Payment/Inventory Service and consumed
// here to drive the choreographed saga (Phase 3). Shapes must match the
// publishing service's own copy exactly - see the namespace note above.

public sealed record PaymentProcessedEvent(
    Guid OrderId,
    Guid TransactionId,
    decimal Amount,
    DateTime Timestamp);

public sealed record PaymentFailedEvent(
    Guid OrderId,
    string Reason,
    DateTime Timestamp);

public sealed record InventoryReservedEvent(
    Guid OrderId,
    IReadOnlyCollection<InventoryReservedEventItem> Items,
    DateTime Timestamp);

public sealed record InventoryReservedEventItem(Guid ProductId, int Quantity);

public sealed record InventoryFailedEvent(
    Guid OrderId,
    string Reason,
    DateTime Timestamp);
