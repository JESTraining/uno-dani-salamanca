using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentService.Infrastructure.ExternalServices;
using PaymentService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PaymentService.Tests.Integration;

/// Test double used only to avoid waiting 2-5 real seconds per payment in
/// integration tests. The gateway's actual rules (fraud/random-chance) stay
/// real - only the delay is skipped.
internal sealed class NoOpPaymentProcessingDelay : IPaymentProcessingDelay
{
    public Task DelayAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("paymentdb_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PaymentDbContext>>();
            services.AddDbContext<PaymentDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()).UseSnakeCaseNamingConvention());

            services.RemoveAll<IPaymentProcessingDelay>();
            services.AddSingleton<IPaymentProcessingDelay, NoOpPaymentProcessingDelay>();
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var schemaScript = await File.ReadAllTextAsync(Path.Combine(FindRepositoryRoot(), "scripts", "payment-service-schema.sql"));

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.ExecuteSqlRawAsync(RemoveRolePrivilegeStatements(schemaScript));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// Locates the repository root from this source file's own path (not
    /// the build output path), so the schema script can be found regardless
    /// of where the test binaries end up.
    private static string FindRepositoryRoot([CallerFilePath] string here = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CLAUDE.md")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root (CLAUDE.md not found).");
    }

    /// The tracked schema script grants privileges to the payment_service
    /// role for the real orders-postgres container (see
    /// scripts/payment-service-schema.sql). That role does not exist in
    /// this Testcontainers instance, which the tests connect to directly as
    /// postgres - already the owner with full access - so those statements
    /// would just fail here and are stripped before executing the rest.
    private static string RemoveRolePrivilegeStatements(string script) =>
        string.Join('\n', script
            .Split('\n')
            .Where(line => !line.TrimStart().StartsWith("GRANT ", StringComparison.OrdinalIgnoreCase)
                         && !line.TrimStart().StartsWith("ALTER DEFAULT PRIVILEGES", StringComparison.OrdinalIgnoreCase)));
}
