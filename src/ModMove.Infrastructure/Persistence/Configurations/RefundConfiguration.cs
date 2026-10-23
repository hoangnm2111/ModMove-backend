using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;
using ModMove.Infrastructure.Persistence.Conversions;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds");
        builder.Property(r => r.Reason).HasMaxLength(1000);
        builder.Property(r => r.EvidenceUrls).HasJsonList();
        builder.Property(r => r.RequestedAmount).HasPrecision(15, 2);
        builder.Property(r => r.ProposedAmount).HasPrecision(15, 2);
        builder.Property(r => r.ProposalReason).HasMaxLength(1000);
        builder.Property(r => r.InfoRequestNote).HasMaxLength(1000);
        builder.Property(r => r.CustomerReply).HasMaxLength(1000); // DECISIONS 7.4
        builder.Property(r => r.BankReferenceCode).HasMaxLength(50);

        // "One open refund per (order, source)" and "one complaint per order" are checked by the
        // command handlers: MySQL has no partial unique index.
        builder.HasIndex(r => new { r.OrderId, r.SourceType });

        builder.HasOne<ShippingOrder>().WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InTransitIncident>().WithMany().HasForeignKey(r => r.IncidentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.ProposedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
