using InventoryService.Application.Abstractions;
using InventoryService.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly InventoryDbContext _context;

    public UnitOfWork(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("The record was modified by another request.");
        }
    }
}
