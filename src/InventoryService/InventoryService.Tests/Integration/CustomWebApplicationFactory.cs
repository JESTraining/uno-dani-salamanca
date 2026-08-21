using System.Runtime.CompilerServices;
using InventoryService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace InventoryService.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("inventorydb_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<InventoryDbContext>>();
            services.AddDbContext<InventoryDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()).UseSnakeCaseNamingConvention());
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var schemaScript = await File.ReadAllTextAsync(Path.Combine(FindRepositoryRoot(), "scripts", "inventory-service-schema.sql"));

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await db.Database.ExecuteSqlRawAsync(RemoveRolePrivilegeStatements(schemaScript));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    private static string FindRepositoryRoot([CallerFilePath] string here = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CLAUDE.md")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root (CLAUDE.md not found).");
    }

    /// The tracked schema script grants privileges to the inventory_service
    /// role for the real orders-postgres container (see
    /// scripts/inventory-service-schema.sql). That role does not exist in
    /// this Testcontainers instance, which the tests connect to directly as
    /// postgres - already the owner with full access - so those statements
    /// would just fail here and are stripped before executing the rest.
    private static string RemoveRolePrivilegeStatements(string script) =>
        string.Join('\n', script
            .Split('\n')
            .Where(line => !line.TrimStart().StartsWith("GRANT ", StringComparison.OrdinalIgnoreCase)
                         && !line.TrimStart().StartsWith("ALTER DEFAULT PRIVILEGES", StringComparison.OrdinalIgnoreCase)));
}
