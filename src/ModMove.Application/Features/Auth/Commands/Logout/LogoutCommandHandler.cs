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
        var user = await CurrentUser.LoadAsync(currentUser, users, cancellationToken);
        user.SetFcmToken(null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
