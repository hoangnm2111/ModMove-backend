using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Auth.Commands.Login;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth.Commands.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IJwtTokenService tokenService,
    TimeProvider clock) : ICommandHandler<RefreshTokenCommand, LoginResponse>
{
    private const string SignInAgain = "Sign in again.";

    public async Task<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var stored = await refreshTokens.GetByHashAsync(RefreshTokens.Hash(request.RefreshToken), cancellationToken)
            ?? throw new UnauthorizedException(SignInAgain);

        if (stored.IsExpired(now))
            throw new UnauthorizedException(SignInAgain);

        // An already rotated token coming back means someone else has a copy: sign the user out everywhere.
        // A token revoked by logout or a password change is only refused, so a stale device cannot end the new session.
        if (stored.ReplacedAt is not null)
        {
            await refreshTokens.RevokeAllForUserAsync(stored.UserId, now, cancellationToken);
            throw new UnauthorizedException(SignInAgain);
        }

        if (stored.RevokedAt is not null)
            throw new UnauthorizedException(SignInAgain);

        var user = await users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || user.AccountStatus != AccountStatus.Active || !user.EmailVerified)
            throw new UnauthorizedException(SignInAgain);

        stored.Rotate(now);
        var next = RefreshTokens.Issue(refreshTokens, user.Id, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponse(tokenService.CreateAccessToken(user.Id, user.Role), next, user.Id, user.FullName, user.Role);
    }
}
