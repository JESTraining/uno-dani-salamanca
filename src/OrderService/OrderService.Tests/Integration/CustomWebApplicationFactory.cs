using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderService.Application.Abstractions;
using OrderService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace OrderService.Tests.Integration;

/// Stub used only in tests: the real Inventory Service does not exist until
/// Phase 2, so integration tests assume stock is always available and focus
/// on Order Service's own behavior.
public class AlwaysAvailableInventoryChecker : IInventoryAvailabilityChecker
{
    public Task<bool> IsStockAvailableAsync(IReadOnlyCollection<StockCheckItem> items, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("orderdb_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderDbContext>>();
            services.AddDbContext<OrderDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()).UseSnakeCaseNamingConvention());

            services.RemoveAll<IInventoryAvailabilityChecker>();
            services.AddScoped<IInventoryAvailabilityChecker, AlwaysAvailableInventoryChecker>();
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
