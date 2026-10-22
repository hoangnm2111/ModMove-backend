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
}
