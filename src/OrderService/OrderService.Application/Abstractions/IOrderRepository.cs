using OrderService.Application.Contracts;
using OrderService.Domain;

namespace OrderService.Application.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(OrderListQuery query, CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);
}
