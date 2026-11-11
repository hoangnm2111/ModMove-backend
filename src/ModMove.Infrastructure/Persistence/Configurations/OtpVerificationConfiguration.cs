using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Configurations;

internal sealed class OtpVerificationConfiguration : IEntityTypeConfiguration<OtpVerification>
{
    public void Configure(EntityTypeBuilder<OtpVerification> builder)
    {
        builder.ToTable("otp_verifications");
        builder.Property(o => o.Email).HasMaxLength(100);
        builder.Property(o => o.CodeHash).HasMaxLength(60);

        // A-02: registration details held until the email is verified; same sizes as the users columns they become.
        builder.Property(o => o.FullName).HasMaxLength(100);
        builder.Property(o => o.PhoneNumber).HasMaxLength(15);
        builder.Property(o => o.PasswordHash).HasMaxLength(60);

        // Two requests with the same code, or parallel wrong guesses, must not both win: the loser gets 409.
        builder.Property(o => o.VerifiedAt).IsConcurrencyToken();
        builder.Property(o => o.FailedAttempts).IsConcurrencyToken();

        // A code replaced meanwhile cannot verify, and parallel resends of a live code cannot lose a count (GB-25): the loser gets 409.
        builder.Property(o => o.CodeHash).IsConcurrencyToken();

        // GB-25: one row per email and purpose, so two first sends in parallel cannot both be saved and mailed.
        builder.HasIndex(o => new { o.Email, o.Purpose }).IsUnique();
    }
}
