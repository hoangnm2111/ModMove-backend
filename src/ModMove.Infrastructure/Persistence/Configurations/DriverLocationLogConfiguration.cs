using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class DriverLocationLogConfiguration : IEntityTypeConfiguration<DriverLocationLog>
{
    public void Configure(EntityTypeBuilder<DriverLocationLog> builder)
    {
        builder.ToTable("driver_location_logs");
        builder.Property(l => l.Latitude).HasPrecision(9, 6);
        builder.Property(l => l.Longitude).HasPrecision(9, 6);

        builder.HasIndex(l => new { l.OrderId, l.RecordedAt });

        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(l => l.DriverProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShippingOrder>().WithMany().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
