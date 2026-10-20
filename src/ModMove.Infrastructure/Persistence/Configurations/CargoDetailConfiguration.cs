using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;
using ModMove.Infrastructure.Persistence.Conversions;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class CargoDetailConfiguration : IEntityTypeConfiguration<CargoDetail>
{
    public void Configure(EntityTypeBuilder<CargoDetail> builder)
    {
        builder.ToTable("cargo_details");
        builder.Property(c => c.CargoType).HasMaxLength(50);
        builder.Property(c => c.WeightKg).HasPrecision(8, 2);
        builder.Property(c => c.PhotoUrls).HasJsonList();
        builder.Property(c => c.SpecialNotes).HasMaxLength(500);

        builder.HasIndex(c => c.OrderId).IsUnique();
    }
}
