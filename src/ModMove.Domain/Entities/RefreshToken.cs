using ModMove.Domain.Common;
using ModMove.Domain.Constants;

namespace ModMove.Domain.Entities;

/// <summary>
/// Long-lived sign-in (DECISIONS 9.1): 30 days, stored only as a SHA-256 hash, used once and then replaced.
/// A token that was already <b>rotated</b> coming back means it leaked, so the caller revokes every token of
/// that user. A token revoked for another reason (logout, password change) is just refused.
/// </summary>
public sealed class RefreshToken : BaseCreatedEntity, IAggregateRoot
{
    private RefreshToken() { }

    public long UserId { get; private set; }

    /// <summary>Hex SHA-256 of the token the client holds; the plain token is never stored.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Set only when the token was exchanged for a new one; tells reuse apart from a normal revoke.</summary>
    public DateTimeOffset? ReplacedAt { get; private set; }

    public static RefreshToken Issue(long userId, string tokenHash, DateTimeOffset now) =>
        new() { UserId = userId, TokenHash = tokenHash, ExpiresAt = now + BusinessRules.RefreshTokenLifetime };

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>Logout, password change or reset, account deletion or lock.</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>Single use: this token was exchanged for a new pair.</summary>
    public void Rotate(DateTimeOffset now)
    {
        RevokedAt ??= now;
        ReplacedAt = now;
    }
}
