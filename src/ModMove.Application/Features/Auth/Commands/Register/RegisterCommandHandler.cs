using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth.Commands.Register;

internal sealed class RegisterCommandHandler(
    IUserRepository users,
    IOtpRepository otps,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    ILogger<RegisterCommandHandler> logger,
    TimeProvider clock) : ICommandHandler<RegisterCommand, RegisterResponse>
{
    public async Task<RegisterResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = AuthRules.Normalize(request.Email);
        var phone = request.PhoneNumber.Trim();
        var fullName = request.FullName.Trim();
        var code = AuthRules.NewOtpCode();

        // GB-01: accounts are saved only once verified (A-02), so any account with this email or phone is taken.
        if (await users.IsEmailOrPhoneTakenAsync(email, phone, cancellationToken))
            throw await AuthRules.AlreadyRegisteredAsync(users, email, cancellationToken);

        // GB-25 limit reached: send nothing and change nothing, answer like a normal registration.
        var otp = await AuthRules.TryIssueOtpAsync(otps, email, OtpPurpose.Register, passwordHasher.Hash(code), clock.GetUtcNow(), cancellationToken);
        if (otp is null)
            return new RegisterResponse(email);

        otp.HoldRegistration(fullName, phone, passwordHasher.Hash(request.Password), request.Role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await AuthRules.SendOtpAsync(emailSender, email, code, OtpPurpose.Register, logger, cancellationToken);

        return new RegisterResponse(email);
    }
}
