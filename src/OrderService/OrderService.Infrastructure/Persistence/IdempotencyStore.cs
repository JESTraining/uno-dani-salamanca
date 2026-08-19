using Microsoft.EntityFrameworkCore;
using OrderService.Application.Abstractions;

namespace OrderService.Infrastructure.Persistence;

public class IdempotencyStore : IIdempotencyStore
{
    private readonly OrderDbContext _context;

    public IdempotencyStore(OrderDbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> FindExistingOrderIdAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var record = await _context.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == idempotencyKey, cancellationToken);

        return record?.OrderId;
    }

    public async Task SaveAsync(string idempotencyKey, Guid orderId, CancellationToken cancellationToken) =>
        await _context.IdempotencyKeys.AddAsync(
            new IdempotencyKeyRecord { Key = idempotencyKey, OrderId = orderId, CreatedAt = DateTime.UtcNow },
            cancellationToken);
}
