namespace OrderService.Application.IntegrationEvents;

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
