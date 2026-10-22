using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth.Commands.ResetPassword;

internal sealed class ResetPasswordCommandHandler(
    IUserRepository users,
    IOtpRepository otps,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IRefreshTokenRepository refreshTokens,
    TimeProvider clock) : ICommandHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = AuthRules.Normalize(request.Email);
        var user = await users.GetByEmailAsync(email, cancellationToken)
            ?? throw OtpCheck.Invalid();

        await OtpCheck.VerifyAsync(otps, unitOfWork, passwordHasher, email, OtpPurpose.ResetPassword, request.Code, clock.GetUtcNow(), cancellationToken);

        user.ChangePassword(passwordHasher.Hash(request.NewPassword));
        user.VerifyEmail();            // the code just proved the user owns this email
        user.RegisterSuccessfulLogin(); // lift a GB-02 lock: the owner has proven who they are
        await refreshTokens.RevokeAllForUserAsync(user.Id, clock.GetUtcNow(), cancellationToken); // 9.1
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
