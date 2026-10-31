using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.Logout;

internal sealed class LogoutCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Signing out must always work, even for a deactivated or locked account: no active-status check here.
        var user = await users.GetByIdAsync(currentUser.RequireUserId(), cancellationToken);
        if (user is null)
            return;

        // 9.2: only this device's token is cleared; another phone that registered later keeps its pushes.
        if (!string.IsNullOrEmpty(request.FcmToken) && user.FcmToken == request.FcmToken)
            user.SetFcmToken(null);
        await refreshTokens.RevokeAllForUserAsync(user.Id, clock.GetUtcNow(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
