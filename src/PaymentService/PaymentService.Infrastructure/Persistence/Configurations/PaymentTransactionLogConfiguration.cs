using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain;

namespace PaymentService.Infrastructure.Persistence.Configurations;

public class PaymentTransactionLogConfiguration : IEntityTypeConfiguration<PaymentTransactionLog>
{
    public void Configure(EntityTypeBuilder<PaymentTransactionLog> builder)
    {
        builder.ToTable("payment_transaction_logs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(l => l.Amount).HasColumnType("numeric(12,2)");
        builder.Property(l => l.FailureReason).HasMaxLength(200);

        builder.HasIndex(l => new { l.PaymentId, l.AttemptNumber }).IsUnique();

        // No navigation property on either side by design (the log is an
        // audit trail, not part of the Payment aggregate's public shape),
        // but EF Core still needs the FK relationship declared so it inserts
        // Payment before PaymentTransactionLog when both are new in the same
        // SaveChanges call - without this, insert ordering is undefined and
        // intermittently violates payment_transaction_logs_payment_id_fkey.
        builder.HasOne<Payment>().WithMany().HasForeignKey(l => l.PaymentId);
    }
}
