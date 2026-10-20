using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class VehicleTypeConfiguration : IEntityTypeConfiguration<VehicleType>
{
    public void Configure(EntityTypeBuilder<VehicleType> builder)
    {
        builder.ToTable("vehicle_types");
        builder.Property(v => v.Name).HasMaxLength(50);
        builder.Property(v => v.MaxLoadWeightKg).HasPrecision(8, 2);
        builder.Property(v => v.BaseFare).HasPrecision(15, 2);
        builder.Property(v => v.PricePerKm).HasPrecision(15, 2);
        builder.Property(v => v.MinimumFare).HasPrecision(15, 2);

        builder.HasIndex(v => v.Name).IsUnique();
    }
}
