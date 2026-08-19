namespace OrderService.Application.IntegrationEvents;

/// Wire contract fixed in CLAUDE.md ("Arquitectura: Reglas No Negociables"). Any
/// field change here must be mirrored there in the same change.
public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    IReadOnlyCollection<OrderCreatedEventItem> Items,
    DateTime Timestamp);

public sealed record OrderCreatedEventItem(Guid ProductId, int Quantity);

/// Wire contract fixed in CLAUDE.md ("Arquitectura: Reglas No Negociables").
public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    string PreviousStatus,
    string NewStatus,
    DateTime Timestamp);
