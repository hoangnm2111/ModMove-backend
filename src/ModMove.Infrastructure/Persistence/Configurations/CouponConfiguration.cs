using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons");
        builder.Property(c => c.DiscountCode).HasMaxLength(30);
        builder.Property(c => c.DiscountValue).HasPrecision(15, 2);
        builder.Property(c => c.MinimumOrderValue).HasPrecision(15, 2);

        builder.HasIndex(c => c.DiscountCode).IsUnique();
    }
}
