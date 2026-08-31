using InventoryService.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InventoryService.Infrastructure.BackgroundServices;

/// Minimal periodic sweep for the 5-minute reservation timeout rule (see
/// CLAUDE.md, "Critical Business Rules" and "Key Design Decisions" - this
/// is the "never violate" rule, shipped now as a plain timer; a fuller job
/// framework remains a bonus item, out of scope for this phase).
public class ExpiredReservationCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredReservationCleanupService> _logger;

    public ExpiredReservationCleanupService(IServiceScopeFactory scopeFactory, ILogger<ExpiredReservationCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reservationManager = scope.ServiceProvider.GetRequiredService<IReservationManager>();

                var released = await reservationManager.ReleaseExpiredReservationsAsync(stoppingToken);
                if (released > 0)
                    _logger.LogInformation("Released {Count} expired inventory reservations.", released);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error while releasing expired inventory reservations.");
            }
        }
    }
}
