using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");
        builder.Property(v => v.LicensePlate).HasMaxLength(15);
        builder.Property(v => v.MaxLoadWeightKg).HasPrecision(8, 2);
        builder.Property(v => v.CargoBedDimensions).HasMaxLength(50);
        builder.Property(v => v.RegistrationCertUrl).HasMaxLength(500);
        builder.Property(v => v.InspectionCertUrl).HasMaxLength(500);
        builder.Property(v => v.VehiclePhotoUrl).HasMaxLength(500);

        builder.HasIndex(v => v.LicensePlate).IsUnique(); // GB-04
        builder.HasOne<VehicleType>().WithMany().HasForeignKey(v => v.VehicleTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
