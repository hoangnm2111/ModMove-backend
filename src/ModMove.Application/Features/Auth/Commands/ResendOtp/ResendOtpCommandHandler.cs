using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
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

        var code = AuthRules.NewOtpCode();
        if (!await AuthRules.TryIssueOtpAsync(otps, email, request.Purpose, passwordHasher.Hash(code), clock.GetUtcNow(), cancellationToken))
            return; // GB-25 limit reached: send nothing, answer like an unknown email (no 422 that would reveal the account)

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await AuthRules.SendOtpAsync(emailSender, email, code, request.Purpose, logger, cancellationToken);
    }
}
