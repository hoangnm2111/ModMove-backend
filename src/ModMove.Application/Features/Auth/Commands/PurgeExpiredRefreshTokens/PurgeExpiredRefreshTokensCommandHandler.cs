using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.PurgeExpiredRefreshTokens;

/// <summary>
/// Only expired tokens go: an expired token is refused anyway, while a live rotated one must stay
/// so its reuse still signs the user out everywhere (9.1). No IUnitOfWork: the repository deletes in bulk.
/// </summary>
internal sealed class PurgeExpiredRefreshTokensCommandHandler(IRefreshTokenRepository refreshTokens, TimeProvider clock)
    : ICommandHandler<PurgeExpiredRefreshTokensCommand, int>
{
    public Task<int> Handle(PurgeExpiredRefreshTokensCommand request, CancellationToken cancellationToken) =>
        refreshTokens.DeleteExpiredAsync(clock.GetUtcNow(), cancellationToken);
}
