using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("ratings", t => t.HasCheckConstraint("ck_ratings_stars", "rating_stars BETWEEN 1 AND 5"));
        builder.Property(r => r.RatingStars).HasColumnType("tinyint");
        builder.Property(r => r.ReviewComment).HasMaxLength(1000);

        builder.HasIndex(r => r.OrderId).IsUnique(); // GB-20: one rating per trip

        builder.HasOne<ShippingOrder>().WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(r => r.DriverProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}
