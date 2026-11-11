using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions", t =>
            t.HasCheckConstraint("ck_payment_transactions_one_target", "(order_id IS NULL) <> (wallet_id IS NULL)"));
        builder.Property(p => p.PaymentMethod).HasMaxLength(10);
        builder.Property(p => p.PayerRole).HasMaxLength(10);
        builder.Property(p => p.Amount).HasPrecision(15, 2);
        builder.Property(p => p.CheckoutUrl).HasMaxLength(500);
        builder.Property(p => p.PayerBankBin).HasMaxLength(20);
        builder.Property(p => p.PayerAccountNumber).HasMaxLength(19);
        builder.Property(p => p.PayerAccountName).HasMaxLength(100);
        // Optimistic concurrency on TransactionStatus prevents a late JOB-PAY-01 tick or duplicate link creation from overwriting Paid.
        builder.Property(p => p.TransactionStatus).IsConcurrencyToken();

        builder.HasIndex(p => p.PayosOrderCode).IsUnique();

        builder.HasOne<ShippingOrder>().WithMany().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DriverWallet>().WithMany().HasForeignKey(p => p.WalletId).OnDelete(DeleteBehavior.Restrict);
    }
}
