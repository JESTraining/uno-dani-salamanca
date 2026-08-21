using InventoryService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Infrastructure.Persistence.Configurations;

/// Maps to the products table created in Phase 1 by
/// scripts/inventory-service-schema.sql. No EF migration is generated for
/// this service - see CLAUDE.md, "Key Design Decisions".
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Sku).IsRequired().HasMaxLength(50);
        builder.HasIndex(p => p.Sku).IsUnique();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.UnitPrice).HasColumnType("numeric(12,2)");

        builder.Ignore(p => p.AvailableQuantity);

        // The existing schema's plain "version" INT column (not Postgres
        // xmin - see CLAUDE.md) is the concurrency token. The application
        // increments it itself in Product's own methods; EF Core adds
        // "WHERE version = @original" to every UPDATE and throws
        // DbUpdateConcurrencyException on a mismatch.
        builder.Property(p => p.Version).IsConcurrencyToken();
    }
}
