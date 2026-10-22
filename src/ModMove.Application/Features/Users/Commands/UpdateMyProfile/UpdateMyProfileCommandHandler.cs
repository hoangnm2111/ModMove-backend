using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Commands.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateMyProfileCommand>
{
    public async Task Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);
        var phone = request.PhoneNumber.Trim();

        if (phone != user.PhoneNumber && await users.IsPhoneTakenByAnotherAsync(phone, user.Id, cancellationToken))
            throw new ConflictException("This phone number is already registered."); // GB-01

        user.UpdateProfile(request.FullName.Trim(), phone, request.AvatarUrl);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
