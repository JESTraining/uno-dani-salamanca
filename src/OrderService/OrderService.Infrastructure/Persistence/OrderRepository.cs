using Microsoft.EntityFrameworkCore;
using OrderService.Application.Abstractions;
using OrderService.Application.Contracts;
using OrderService.Domain;

namespace OrderService.Infrastructure.Persistence;

public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;

    public OrderRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(OrderListQuery query, CancellationToken cancellationToken)
    {
        var filtered = _context.Orders.Include(o => o.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<OrderStatus>(query.Status, true, out var status))
            filtered = filtered.Where(o => o.Status == status);

        if (query.DateFrom is not null)
            filtered = filtered.Where(o => o.CreatedAt >= query.DateFrom);

        if (query.DateTo is not null)
            filtered = filtered.Where(o => o.CreatedAt <= query.DateTo);

        if (query.CustomerId is not null)
            filtered = filtered.Where(o => o.CustomerId == query.CustomerId);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await filtered
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken) =>
        await _context.Orders.AddAsync(order, cancellationToken);
}
