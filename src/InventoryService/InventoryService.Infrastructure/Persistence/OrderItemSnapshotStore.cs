using InventoryService.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure.Persistence;

public class OrderItemSnapshotStore : IOrderItemSnapshotStore
{
    private readonly InventoryDbContext _context;

    public OrderItemSnapshotStore(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(Guid orderId, IReadOnlyCollection<OrderItemSnapshotEntry> items, CancellationToken cancellationToken)
    {
        var alreadyRecorded = await _context.OrderItemSnapshots.AnyAsync(s => s.OrderId == orderId, cancellationToken);
        if (alreadyRecorded)
            return;

        var now = DateTime.UtcNow;
        var records = items.Select(item => new OrderItemSnapshotRecord
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            CreatedAt = now
        });

        await _context.OrderItemSnapshots.AddRangeAsync(records, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderItemSnapshotEntry>> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _context.OrderItemSnapshots
            .Where(s => s.OrderId == orderId)
            .Select(s => new OrderItemSnapshotEntry(s.ProductId, s.Quantity))
            .ToListAsync(cancellationToken);
}
