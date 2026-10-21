using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class SupportRequestConfiguration : IEntityTypeConfiguration<SupportRequest>
{
    public void Configure(EntityTypeBuilder<SupportRequest> builder)
    {
        builder.ToTable("support_requests");
        builder.Property(s => s.FullName).HasMaxLength(100);
        builder.Property(s => s.Contact).HasMaxLength(100);
        builder.Property(s => s.Content).HasMaxLength(1000);

        builder.HasIndex(s => s.Status);
    }
}
