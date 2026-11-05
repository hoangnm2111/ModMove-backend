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
        builder.Property(u => u.FcmToken).HasMaxLength(255);

        // GB-02: parallel wrong-password requests must not overwrite each other's count (lost update).
        // The loser's save fails with ConflictException (409) instead of resetting the counter.
        builder.Property(u => u.FailedLoginCount).IsConcurrencyToken();

        // A-02: registering again read the account as unverified; if it was verified meanwhile, the loser gets 409.
        builder.Property(u => u.EmailVerified).IsConcurrencyToken();

        builder.HasIndex(u => u.PhoneNumber).IsUnique(); // GB-01
        builder.HasIndex(u => u.Email).IsUnique();       // GB-01
    }
}
