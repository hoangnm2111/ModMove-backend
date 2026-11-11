using ModMove.Domain.Common;
using ModMove.Domain.Constants;
using ModMove.Domain.Enums;

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

    /// <summary>Wrong guesses against the current code; at the limit the code is dead (brute-force guard).</summary>
    public int FailedAttempts { get; private set; }

    // A-02: what a Register code will create; the account is saved only once the email is verified.
    public string? FullName { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? PasswordHash { get; private set; }
    public UserRole? Role { get; private set; }

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

    /// <summary>
    /// Replace the code with a new one; at most 3 sends per 15 minutes (GB-25).
    /// Returns false at the limit instead of throwing, so callers can answer the same way as for unknown emails.
    /// </summary>
    public bool TryResend(string codeHash, DateTimeOffset now)
    {
        if (now - WindowStartedAt >= BusinessRules.OtpResendWindow)
        {
            WindowStartedAt = now;
            SendCount = 0;
        }

        if (SendCount >= BusinessRules.OtpMaxSendsPerWindow)
            return false;

        SendCount++;
        CodeHash = codeHash;
        ExpiresAt = now + BusinessRules.OtpLifetime;
        VerifiedAt = null;
        FailedAttempts = 0;
        return true;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>Not used yet, not expired, and not burnt by too many wrong guesses.</summary>
    public bool IsUsable(DateTimeOffset now) =>
        VerifiedAt is null && !IsExpired(now) && FailedAttempts < BusinessRules.OtpMaxFailedAttempts;

    public void RegisterFailedAttempt() => FailedAttempts++;

    public void MarkVerified(DateTimeOffset now) => VerifiedAt = now;

    /// <summary>Registering again replaces the held details: the latest registration wins (A-02).</summary>
    public void HoldRegistration(string fullName, string phoneNumber, string passwordHash, UserRole role)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
        Role = role;
    }

    /// <summary>The code proved the mailbox: build the verified account, use up the code and forget the held details.</summary>
    public User CompleteRegistration(DateTimeOffset now)
    {
        if (FullName is null || PhoneNumber is null || PasswordHash is null || Role is null)
            throw new InvalidOperationException("No registration is waiting for this code.");

        var user = User.Create(FullName, PhoneNumber, Email, PasswordHash, Role.Value, emailVerified: true);
        MarkVerified(now);
        FullName = PhoneNumber = PasswordHash = null;
        Role = null;
        return user;
    }
}
