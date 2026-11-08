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

        // Two requests with the same code, or parallel wrong guesses, must not both win: the loser gets 409.
        builder.Property(o => o.VerifiedAt).IsConcurrencyToken();
        builder.Property(o => o.FailedAttempts).IsConcurrencyToken();

        // A code replaced meanwhile cannot verify, and parallel resends of a live code cannot lose a count (GB-25): the loser gets 409.
        builder.Property(o => o.CodeHash).IsConcurrencyToken();

        builder.HasIndex(o => o.Email);
    }
}
