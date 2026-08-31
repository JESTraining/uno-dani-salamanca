using OrderService.Application.Abstractions;

namespace OrderService.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly OrderDbContext _context;

    public UnitOfWork(OrderDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
