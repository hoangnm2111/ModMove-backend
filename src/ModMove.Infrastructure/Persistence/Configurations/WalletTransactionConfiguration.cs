using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions");
        builder.Property(t => t.Amount).HasPrecision(15, 2);
        builder.Property(t => t.BalanceAfter).HasPrecision(15, 2);
        builder.Property(t => t.ReferenceCode).HasMaxLength(50);

        builder.HasIndex(t => t.WalletId);
        builder.HasIndex(t => new { t.OrderId, t.TransactionType }).IsUnique(); // an order settles once

        builder.HasOne<ShippingOrder>().WithMany().HasForeignKey(t => t.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.ProcessedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
