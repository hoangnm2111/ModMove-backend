using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class ShippingOrderConfiguration : IEntityTypeConfiguration<ShippingOrder>
{
    public void Configure(EntityTypeBuilder<ShippingOrder> builder)
    {
        builder.ToTable("shipping_orders");
        builder.Property(o => o.OrderCode).HasMaxLength(20);
        builder.Property(o => o.PickupAddress).HasMaxLength(255);
        builder.Property(o => o.PickupLatitude).HasPrecision(9, 6);
        builder.Property(o => o.PickupLongitude).HasPrecision(9, 6);
        builder.Property(o => o.DropoffAddress).HasMaxLength(255);
        builder.Property(o => o.DropoffLatitude).HasPrecision(9, 6);
        builder.Property(o => o.DropoffLongitude).HasPrecision(9, 6);
        builder.Property(o => o.ReceiverName).HasMaxLength(100);
        builder.Property(o => o.ReceiverPhone).HasMaxLength(15);
        builder.Property(o => o.EstimatedDistanceKm).HasPrecision(8, 2);
        builder.Property(o => o.DistanceFee).HasPrecision(15, 2);
        builder.Property(o => o.ServiceFee).HasPrecision(15, 2);
        builder.Property(o => o.DiscountAmount).HasPrecision(15, 2);
        builder.Property(o => o.TotalFee).HasPrecision(15, 2);
        builder.Property(o => o.PaymentMethod).HasMaxLength(10);
        builder.Property(o => o.PayerRole).HasMaxLength(10);
        builder.Property(o => o.CancellationReason).HasMaxLength(500);
        builder.Property(o => o.CancellationReasonCode).HasMaxLength(50); // master_data_items.code
        builder.Property(o => o.CommissionRate).HasPrecision(5, 4);   // e.g. 0.1000
        builder.Property(o => o.CommissionAmount).HasPrecision(15, 2);
        builder.Property(o => o.RowVersion).IsConcurrencyToken(); // GB-08: first accept wins

        builder.HasIndex(o => o.OrderCode).IsUnique();
        builder.HasIndex(o => o.OrderStatus);

        builder.HasOne<User>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(o => o.DriverProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VehicleType>().WithMany().HasForeignKey(o => o.VehicleTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Coupon>().WithMany().HasForeignKey(o => o.CouponId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Cargo).WithOne().HasForeignKey<CargoDetail>(c => c.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(o => o.Proof).WithOne().HasForeignKey<ProofOfDelivery>(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(o => o.StatusHistories).WithOne().HasForeignKey(h => h.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
