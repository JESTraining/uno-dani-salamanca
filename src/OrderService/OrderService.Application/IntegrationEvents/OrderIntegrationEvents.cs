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
