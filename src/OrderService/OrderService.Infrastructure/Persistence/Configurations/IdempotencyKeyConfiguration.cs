using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrderService.Infrastructure.Persistence.Configurations;

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyRecord> builder)
    {
        builder.ToTable("idempotency_keys");
        builder.HasKey(k => k.Key);
        builder.Property(k => k.Key).HasMaxLength(200);
    }
}
