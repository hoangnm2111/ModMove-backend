using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;
using ModMove.Infrastructure.Persistence.Conversions;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class InTransitIncidentConfiguration : IEntityTypeConfiguration<InTransitIncident>
{
    public void Configure(EntityTypeBuilder<InTransitIncident> builder)
    {
        builder.ToTable("in_transit_incidents");
        builder.Property(i => i.Description).HasMaxLength(1000);
        builder.Property(i => i.PhotoUrls).HasJsonList();
        builder.Property(i => i.Latitude).HasPrecision(9, 6);
        builder.Property(i => i.Longitude).HasPrecision(9, 6);
        builder.Property(i => i.ResolutionNote).HasMaxLength(1000);
        // Q7: one Dispatcher per incident. Two deciding or taking an Open incident at once: the later save is a 409.
        builder.Property(i => i.ResolutionStatus).IsConcurrencyToken();

        builder.HasIndex(i => i.OrderId);

        builder.HasOne<ShippingOrder>().WithMany().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.ReportedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.HandledBy).OnDelete(DeleteBehavior.Restrict);
    }
}
