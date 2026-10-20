using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> builder)
    {
        builder.ToTable("platform_settings");
        builder.Property(s => s.SettingKey).HasMaxLength(50);
        builder.Property(s => s.SettingValue).HasMaxLength(255);

        builder.HasIndex(s => s.SettingKey).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
