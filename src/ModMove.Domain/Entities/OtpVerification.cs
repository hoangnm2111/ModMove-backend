using ModMove.Domain.Common;
using ModMove.Domain.Constants;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>One-time password for registration or password recovery (GB-25).</summary>
public sealed class OtpVerification : BaseCreatedEntity, IAggregateRoot
{
    private OtpVerification() { }

    public string Email { get; private set; } = null!;
    public OtpPurpose Purpose { get; private set; }
    public string CodeHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public int SendCount { get; private set; }
    public DateTimeOffset WindowStartedAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }

    public static OtpVerification Issue(string email, OtpPurpose purpose, string codeHash, DateTimeOffset now) =>
        new()
        {
            Email = email,
            Purpose = purpose,
            CodeHash = codeHash,
            ExpiresAt = now + BusinessRules.OtpLifetime,
            SendCount = 1,
            WindowStartedAt = now,
        };

    /// <summary>Re-send with a new code; at most 3 sends per 15 minutes.</summary>
    public void Resend(string codeHash, DateTimeOffset now)
    {
        if (now - WindowStartedAt >= BusinessRules.OtpResendWindow)
        {
            WindowStartedAt = now;
            SendCount = 0;
        }

        if (SendCount >= BusinessRules.OtpMaxSendsPerWindow)
            throw new DomainException("OTP resend limit reached. Try again later.");

        SendCount++;
        CodeHash = codeHash;
        ExpiresAt = now + BusinessRules.OtpLifetime;
        VerifiedAt = null;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void MarkVerified(DateTimeOffset now) => VerifiedAt = now;
}
