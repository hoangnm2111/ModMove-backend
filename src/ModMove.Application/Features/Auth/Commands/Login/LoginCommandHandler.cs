using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth.Commands.Login;

internal sealed class LoginCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenService tokenService,
    TimeProvider clock) : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim(), cancellationToken);

        // Same answer for "no such email" and "wrong password": do not reveal which emails exist.
        if (user is null)
            throw new UnauthorizedException("Invalid email or password.");

        var now = clock.GetUtcNow();
        if (user.IsLocked(now))
            throw new ForbiddenAccessException($"The account is locked until {user.LockedUntil:u}.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin(now); // GB-02
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (user.AccountStatus != AccountStatus.Active)
            throw new ForbiddenAccessException("The account is not active.");

        user.RegisterSuccessfulLogin();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponse(tokenService.CreateAccessToken(user.Id, user.Role), user.Id, user.FullName, user.Role);
    }
}
