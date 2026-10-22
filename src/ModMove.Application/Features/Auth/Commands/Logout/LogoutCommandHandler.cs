using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.Logout;

internal sealed class LogoutCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IUnitOfWork unitOfWork) : ICommandHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Signing out must always work, even for a deactivated or locked account: no active-status check here.
        var user = await users.GetByIdAsync(currentUser.RequireUserId(), cancellationToken);
        if (user is null)
            return;

        user.SetFcmToken(null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
