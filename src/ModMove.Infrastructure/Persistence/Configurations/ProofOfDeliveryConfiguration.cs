using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;
using ModMove.Infrastructure.Persistence.Conversions;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class ProofOfDeliveryConfiguration : IEntityTypeConfiguration<ProofOfDelivery>
{
    public void Configure(EntityTypeBuilder<ProofOfDelivery> builder)
    {
        builder.ToTable("proof_of_deliveries");
        builder.Property(p => p.PickupPhotoUrls).HasJsonList();
        builder.Property(p => p.RejectionReason).HasMaxLength(500);
        builder.Property(p => p.DeliveryPhotoUrl).HasMaxLength(500);

        builder.HasIndex(p => p.OrderId).IsUnique();
    }
}
