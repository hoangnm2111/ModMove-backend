using ModMove.Application.Common.Exceptions;
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

        // The code comes first, so unknown, verified and waiting emails all get the same answer to a wrong code.
        var now = clock.GetUtcNow();
        var otp = await OtpCheck.MatchAsync(otps, unitOfWork, passwordHasher, email, OtpPurpose.Register, request.Code, now, cancellationToken);
        if (otp.PasswordHash is null || otp.PhoneNumber is null)
            throw OtpCheck.Invalid(); // a code with no registration waiting on it

        // A-02: only after the code, so this answer reaches nobody but the owner of the mailbox. The code stays usable.
        if (!passwordHasher.Verify(request.Password, otp.PasswordHash))
            throw new DomainException("The password does not match the one used to register. Register again with the password you want.", "VERIFY_PASSWORD_MISMATCH");

        // GB-01: another registration with this phone may have been verified first. The unique indexes catch a race (→ 409).
        if (await users.IsEmailOrPhoneTakenAsync(email, otp.PhoneNumber, cancellationToken))
            throw new ConflictException(AuthRules.AlreadyRegistered);

        users.Add(otp.CompleteRegistration(now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
