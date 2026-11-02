using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth.Commands.ResendOtp;

internal sealed class ResendOtpCommandHandler(
    IUserRepository users,
    IOtpRepository otps,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    ILogger<ResendOtpCommandHandler> logger,
    TimeProvider clock) : ICommandHandler<ResendOtpCommand>
{
    public async Task Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        var email = AuthRules.Normalize(request.Email);
        var user = await users.GetByEmailAsync(email, cancellationToken);

        // Nothing to send, but answer the same way so the endpoint does not reveal which emails exist.
        if (user is null || (request.Purpose == OtpPurpose.Register && user.EmailVerified))
            return;

        var now = clock.GetUtcNow();
        var code = AuthRules.NewOtpCode();
        var hash = passwordHasher.Hash(code);
        var latest = await otps.GetLatestAsync(email, request.Purpose, cancellationToken);

        if (latest is null || latest.VerifiedAt is not null)
            otps.Add(OtpVerification.Issue(email, request.Purpose, hash, now));
        else if (!latest.TryResend(hash, now))
            return; // GB-25 limit reached: send nothing, answer like an unknown email (no 422 that would reveal the account)

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await AuthRules.SendOtpAsync(emailSender, email, code, request.Purpose, logger, cancellationToken);
    }
}
