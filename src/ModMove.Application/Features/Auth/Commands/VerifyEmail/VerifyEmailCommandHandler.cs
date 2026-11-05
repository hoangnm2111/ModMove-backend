using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Auth.Commands.VerifyEmail;

internal sealed class VerifyEmailCommandHandler(
    IUserRepository users,
    IOtpRepository otps,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    TimeProvider clock) : ICommandHandler<VerifyEmailCommand>
{
    public async Task Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var email = AuthRules.Normalize(request.Email);
        var user = await users.GetByEmailAsync(email, cancellationToken) ?? throw OtpCheck.Invalid();

        // Always check the code, even for an already verified account: a shortcut here would tell
        // "verified account" apart from "no account".
        var now = clock.GetUtcNow();
        var otp = await OtpCheck.MatchAsync(otps, unitOfWork, passwordHasher, email, OtpPurpose.Register, request.Code, now, cancellationToken);

        // A-02: only after the code, so this answer reaches nobody but the owner of the mailbox. The code stays usable.
        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("The password does not match the one used to register. Reset your password or register again.", "VERIFY_PASSWORD_MISMATCH");

        otp.MarkVerified(now);
        user.VerifyEmail();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
