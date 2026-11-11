using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Exceptions;
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
        // A Register code goes only to a registration still waiting for one: no account yet (A-02).
        var nothingToSend = request.Purpose == OtpPurpose.Register
            ? user is not null || await otps.GetLatestAsync(email, OtpPurpose.Register, cancellationToken) is not { VerifiedAt: null, PasswordHash: not null }
            : user is null;
        if (nothingToSend)
            return;

        var code = AuthRules.NewOtpCode();
        if (await AuthRules.TryIssueOtpAsync(otps, email, request.Purpose, passwordHasher.Hash(code), clock.GetUtcNow(), cancellationToken) is null)
            return; // GB-25 limit reached: send nothing, answer like an unknown email (no 422 that would reveal the account)

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            return; // A parallel request changed this code (a resend or a wrong guess), so the user asks again; a 409 would reveal the account.
        }

        await AuthRules.SendOtpAsync(emailSender, user?.Email ?? email, code, request.Purpose, logger, cancellationToken);
    }
}
