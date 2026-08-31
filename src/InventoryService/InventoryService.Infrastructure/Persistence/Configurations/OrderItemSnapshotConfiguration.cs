using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Infrastructure.Persistence.Configurations;

public class OrderItemSnapshotConfiguration : IEntityTypeConfiguration<OrderItemSnapshotRecord>
{
    public void Configure(EntityTypeBuilder<OrderItemSnapshotRecord> builder)
    {
        builder.ToTable("order_item_snapshots");
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.OrderId, s.ProductId }).IsUnique();
        builder.HasIndex(s => s.OrderId);
    }
}
