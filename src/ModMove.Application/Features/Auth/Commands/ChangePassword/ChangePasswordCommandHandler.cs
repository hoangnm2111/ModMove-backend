using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Auth.Commands.ChangePassword;

internal sealed class ChangePasswordCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher) : ICommandHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await CurrentUser.LoadAsync(currentUser, users, cancellationToken);

        // 422, not 401: a 401 would make the client think the token expired and sign the user out.
        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new DomainException("The current password is wrong.", "WRONG_PASSWORD");

        user.ChangePassword(passwordHasher.Hash(request.NewPassword));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
