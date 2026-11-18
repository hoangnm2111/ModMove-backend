using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class DriverProfileConfiguration : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ToTable("driver_profiles");
        builder.Property(d => d.CitizenIdNumber).HasMaxLength(20).IsRequired(false);
        builder.Property(d => d.DriverLicenseNumber).HasMaxLength(20).IsRequired(false);
        builder.Property(d => d.LicenseFrontUrl).HasMaxLength(500);
        builder.Property(d => d.LicenseBackUrl).HasMaxLength(500);
        builder.Property(d => d.OnlineStatus).HasMaxLength(10);
        builder.Property(d => d.LastLatitude).HasPrecision(9, 6);
        builder.Property(d => d.LastLongitude).HasPrecision(9, 6);
        builder.Property(d => d.BankName).HasMaxLength(100);
        builder.Property(d => d.BankAccountNumber).HasMaxLength(30);
        builder.Property(d => d.BankAccountHolder).HasMaxLength(100);

        builder.HasIndex(d => d.UserId).IsUnique();
        builder.HasIndex(d => d.CitizenIdNumber).IsUnique();
        builder.HasIndex(d => d.DriverLicenseNumber).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(d => d.Applications).WithOne().HasForeignKey(a => a.DriverProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(d => d.Vehicle).WithOne().HasForeignKey<Vehicle>(v => v.DriverProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}
