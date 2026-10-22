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
    IRefreshTokenRepository refreshTokens,
    TimeProvider clock) : ICommandHandler<LoginCommand, LoginResponse>
{
    // One answer for unknown email, wrong password and locked account (GB-02), so the response
    // never tells an attacker which emails exist or that a lock was hit.
    private const string InvalidCredentials = "Invalid email or password, or the account is temporarily locked.";

    // Hash of a random secret: unknown emails still pay for one BCrypt check, so timing does not leak existence.
    private static string? s_dummyHash;

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(AuthRules.Normalize(request.Email), cancellationToken);
        var passwordOk = passwordHasher.Verify(
            request.Password, user?.PasswordHash ?? (s_dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString())));

        if (user is null)
            throw new UnauthorizedException(InvalidCredentials);

        var now = clock.GetUtcNow();
        if (user.IsLocked(now))
            throw new UnauthorizedException(InvalidCredentials); // no counting while locked: the lock already blocks guessing

        if (!passwordOk)
        {
            user.RegisterFailedLogin(now);
            await unitOfWork.SaveChangesAsync(cancellationToken); // saved before the 401 so the count sticks
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (user.AccountStatus != AccountStatus.Active)
            throw new ForbiddenAccessException("The account is not active.", "ACCOUNT_INACTIVE");

        // D1: only after the right password, so this never reveals which emails are registered.
        if (!user.EmailVerified)
            throw new ForbiddenAccessException("Verify your email with the code we sent before signing in.", "EMAIL_NOT_VERIFIED");

        user.RegisterSuccessfulLogin();
        var refreshToken = RefreshTokens.Issue(refreshTokens, user.Id, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponse(tokenService.CreateAccessToken(user.Id, user.Role), refreshToken, user.Id, user.FullName, user.Role);
    }
}
