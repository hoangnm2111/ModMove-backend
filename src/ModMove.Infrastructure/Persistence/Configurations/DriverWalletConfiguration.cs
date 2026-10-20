using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class DriverWalletConfiguration : IEntityTypeConfiguration<DriverWallet>
{
    public void Configure(EntityTypeBuilder<DriverWallet> builder)
    {
        builder.ToTable("driver_wallets");
        builder.Property(w => w.DepositBalance).HasPrecision(15, 2);
        builder.Property(w => w.EarningsBalance).HasPrecision(15, 2);
        builder.Property(w => w.RowVersion).IsConcurrencyToken();

        builder.HasIndex(w => w.DriverProfileId).IsUnique();

        builder.HasOne<DriverProfile>().WithOne().HasForeignKey<DriverWallet>(w => w.DriverProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(w => w.Transactions).WithOne().HasForeignKey(t => t.WalletId).OnDelete(DeleteBehavior.Restrict);
    }
}
