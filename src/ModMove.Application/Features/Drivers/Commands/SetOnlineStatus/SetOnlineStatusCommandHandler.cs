using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Drivers.Commands.SetOnlineStatus;

internal sealed class SetOnlineStatusCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IUnitOfWork unitOfWork) : ICommandHandler<SetOnlineStatusCommand>
{
    public async Task Handle(SetOnlineStatusCommand request, CancellationToken cancellationToken)
    {
        // GB-03 needs an Active account: a token outlives a lock or a deletion (B8).
        var user = await currentUser.LoadUserAsync(users, cancellationToken);

        // No profile means no application was ever submitted, so nothing was approved either.
        var profile = await driverProfiles.GetByUserIdAsync(user.Id, cancellationToken)
            ?? throw new DomainException("Driver documents must be approved before going online.");

        if (request.IsOnline)
            profile.GoOnline(); // the entity refuses a profile that is not Approved (GB-03)
        else
            profile.GoOffline();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

