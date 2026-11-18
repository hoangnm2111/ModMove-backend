using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sign the user out everywhere (password change or reset, deletion, Admin lock, logout, token reuse).
    /// Runs immediately, outside SaveChanges.
    /// </summary>
    Task RevokeAllForUserAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Deletes tokens expired at <paramref name="now"/> (same rule as RefreshToken.IsExpired). Runs immediately, outside SaveChanges.</summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
