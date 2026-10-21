using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class MasterDataItemConfiguration : IEntityTypeConfiguration<MasterDataItem>
{
    public void Configure(EntityTypeBuilder<MasterDataItem> builder)
    {
        builder.ToTable("master_data_items");
        builder.Property(m => m.Category).HasMaxLength(50);
        builder.Property(m => m.Code).HasMaxLength(50);
        builder.Property(m => m.Name).HasMaxLength(100);
        builder.Property(m => m.Description).HasColumnType("text"); // policy content (DECISIONS B13)

        builder.HasIndex(m => new { m.Category, m.Code }).IsUnique();
    }
}
