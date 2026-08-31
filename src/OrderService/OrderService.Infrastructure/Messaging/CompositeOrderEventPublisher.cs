using OrderService.Application.Abstractions;
using OrderService.Domain;

namespace OrderService.Infrastructure.Messaging;

/// Fans every publish call out to all wrapped publishers (RabbitMQ + SignalR
/// today). Takes the publishers as a params array, constructed explicitly in
/// Program.cs rather than resolved via IEnumerable&lt;IOrderEventPublisher&gt; -
/// resolving that way would recursively include this composite itself once
/// it is registered under the same interface.
public class CompositeOrderEventPublisher : IOrderEventPublisher
{
    private readonly IReadOnlyCollection<IOrderEventPublisher> _publishers;

    public CompositeOrderEventPublisher(params IOrderEventPublisher[] publishers)
    {
        _publishers = publishers;
    }

    public Task PublishOrderCreatedAsync(Order order, CancellationToken cancellationToken) =>
        Task.WhenAll(_publishers.Select(p => p.PublishOrderCreatedAsync(order, cancellationToken)));

    public Task PublishOrderStatusChangedAsync(
        Guid orderId,
        OrderStatus previousStatus,
        OrderStatus newStatus,
        CancellationToken cancellationToken) =>
        Task.WhenAll(_publishers.Select(p => p.PublishOrderStatusChangedAsync(orderId, previousStatus, newStatus, cancellationToken)));

    public Task PublishOrderCompletedAsync(Guid orderId, OrderStatus status, CancellationToken cancellationToken) =>
        Task.WhenAll(_publishers.Select(p => p.PublishOrderCompletedAsync(orderId, status, cancellationToken)));
}
