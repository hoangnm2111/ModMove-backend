using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
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

        // GB-01. The unique indexes still catch a race between two requests (→ ConflictException).
        if (await users.IsEmailOrPhoneTakenAsync(email, phone, cancellationToken))
            throw new ConflictException("This email or phone number is already registered.");

        var user = User.Create(request.FullName.Trim(), phone, email, passwordHasher.Hash(request.Password), request.Role);
        users.Add(user);

        var code = AuthRules.NewOtpCode();
        otps.Add(OtpVerification.Issue(email, OtpPurpose.Register, passwordHasher.Hash(code), clock.GetUtcNow()));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await AuthRules.SendOtpAsync(emailSender, email, code, OtpPurpose.Register, logger, cancellationToken);

        return new RegisterResponse(user.Id, email);
    }
}
