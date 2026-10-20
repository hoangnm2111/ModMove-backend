using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.Property(u => u.FullName).HasMaxLength(100);
        builder.Property(u => u.PhoneNumber).HasMaxLength(15);
        builder.Property(u => u.Email).HasMaxLength(100);
        builder.Property(u => u.PasswordHash).HasMaxLength(60);
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.Property(u => u.FcmToken).HasMaxLength(255);

        builder.HasIndex(u => u.PhoneNumber).IsUnique(); // GB-01
        builder.HasIndex(u => u.Email).IsUnique();       // GB-01
    }
}
