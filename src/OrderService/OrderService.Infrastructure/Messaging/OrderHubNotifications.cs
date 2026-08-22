namespace OrderService.Infrastructure.Messaging;

// Transport-owned payloads for the SignalR hub, distinct from the RabbitMQ
// IntegrationEvents and from OrderDtos.cs - the frontend's real-time
// contract is not required to match either 1:1.

public sealed record OrderCreatedNotification(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt);

public sealed record OrderStatusChangedNotification(
    Guid OrderId,
    string PreviousStatus,
    string NewStatus,
    DateTime Timestamp);

public sealed record OrderCompletedNotification(
    Guid OrderId,
    string Status,
    DateTime Timestamp);
