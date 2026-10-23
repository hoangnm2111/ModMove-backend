using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

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
        await OtpCheck.VerifyAsync(otps, unitOfWork, passwordHasher, email, OtpPurpose.Register, request.Code, clock.GetUtcNow(), cancellationToken);
        user.VerifyEmail();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
