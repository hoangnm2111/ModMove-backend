using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Commands.UpdateFcmToken;

internal sealed class UpdateFcmTokenCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateFcmTokenCommand>
{
    public async Task Handle(UpdateFcmTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);
        var token = request.Token.Trim();

        await users.ReleaseFcmTokenAsync(token, user.Id, cancellationToken);
        user.SetFcmToken(token);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
