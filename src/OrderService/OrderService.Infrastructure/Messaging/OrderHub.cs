using Microsoft.AspNetCore.SignalR;

namespace OrderService.Infrastructure.Messaging;

/// Server-to-client push only, no client-invokable methods. Broadcasts to
/// all connected clients (no per-order groups) - the frontend filters by
/// orderId itself, which is sufficient at this project's scale.
public class OrderHub : Hub
{
}
