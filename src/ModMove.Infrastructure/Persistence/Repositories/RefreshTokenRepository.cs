using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(ApplicationDbContext dbContext)
    : Repository<RefreshToken>(dbContext), IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task RevokeAllForUserAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Set.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
}
