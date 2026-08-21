using InventoryService.Domain;

namespace InventoryService.Application.Abstractions;

public interface IReservationRepository
{
    Task<bool> AnyForOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InventoryReservation>> GetExpiredAsync(DateTime asOf, CancellationToken cancellationToken);

    Task AddAsync(InventoryReservation reservation, CancellationToken cancellationToken);
}
