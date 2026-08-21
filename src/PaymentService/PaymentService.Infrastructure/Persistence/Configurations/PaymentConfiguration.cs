using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain;

namespace PaymentService.Infrastructure.Persistence.Configurations;

/// Maps to the payments table created in Phase 1 by
/// scripts/payment-service-schema.sql. No EF migration is generated for
/// this service - this configuration must match that existing schema
/// exactly (see CLAUDE.md, "Key Design Decisions").
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasColumnType("numeric(12,2)");
        builder.Property(p => p.PaymentMethod).IsRequired().HasMaxLength(30);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.FailureReason).HasMaxLength(200);

        builder.HasIndex(p => p.OrderId).IsUnique();
        builder.HasIndex(p => p.Status);
    }
}
