using InventoryService.Application.Abstractions;
using InventoryService.Domain;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure.Persistence;

public class ReservationRepository : IReservationRepository
{
    private readonly InventoryDbContext _context;

    public ReservationRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<bool> AnyForOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _context.InventoryReservations.AnyAsync(r => r.OrderId == orderId, cancellationToken);

    public async Task<IReadOnlyList<InventoryReservation>> GetExpiredAsync(DateTime asOf, CancellationToken cancellationToken) =>
        await _context.InventoryReservations
            .Where(r => r.Status == ReservationStatus.Reserved && r.ExpiresAt <= asOf)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(InventoryReservation reservation, CancellationToken cancellationToken) =>
        await _context.InventoryReservations.AddAsync(reservation, cancellationToken);
}
