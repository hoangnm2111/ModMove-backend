using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;
using ModMove.Infrastructure.Persistence.Conversions;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class DriverApplicationConfiguration : IEntityTypeConfiguration<DriverApplication>
{
    public void Configure(EntityTypeBuilder<DriverApplication> builder)
    {
        builder.ToTable("driver_applications");
        builder.Property(a => a.LicensePlate).HasMaxLength(15);
        builder.Property(a => a.MaxLoadWeightKg).HasPrecision(8, 2);
        builder.Property(a => a.DocumentUrls).HasJsonList();
        builder.Property(a => a.RejectionReasons).HasJsonList();
        builder.Property(a => a.RejectedItems).HasJsonList<RejectedApplicationItem>();
        // Two Administrators approving and rejecting the same application at once: the later save is a 409.
        builder.Property(a => a.ApplicationStatus).IsConcurrencyToken();

        builder.HasIndex(a => new { a.DriverProfileId, a.VersionNumber }).IsUnique();

        builder.HasOne<VehicleType>().WithMany().HasForeignKey(a => a.VehicleTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
