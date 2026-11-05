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
        var fullName = request.FullName.Trim();
        var code = AuthRules.NewOtpCode();

        var user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is not null)
        {
            // DECISIONS A-02: the owner of an unverified account gets a new code instead of a conflict.
            if (!user.CanRegisterAgainAs(request.Role) || await users.IsPhoneTakenByAnotherAsync(phone, user.Id, cancellationToken))
                throw new ConflictException(AlreadyRegistered);

            // GB-25 limit reached: send nothing and change nothing, answer like a normal registration.
            if (!await AuthRules.TryIssueOtpAsync(otps, email, OtpPurpose.Register, passwordHasher.Hash(code), clock.GetUtcNow(), cancellationToken))
                return new RegisterResponse(user.Id, email);

            user.RegisterAgain(fullName, phone, passwordHasher.Hash(request.Password));
        }
        else
        {
            // GB-01. The unique indexes still catch a race between two requests (→ ConflictException).
            if (await users.IsEmailOrPhoneTakenAsync(email, phone, cancellationToken))
                throw new ConflictException(AlreadyRegistered);

            user = User.Create(fullName, phone, email, passwordHasher.Hash(request.Password), request.Role);
            users.Add(user);
            otps.Add(OtpVerification.Issue(email, OtpPurpose.Register, passwordHasher.Hash(code), clock.GetUtcNow()));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await AuthRules.SendOtpAsync(emailSender, user.Email, code, OtpPurpose.Register, logger, cancellationToken);

        return new RegisterResponse(user.Id, email);
    }

    private const string AlreadyRegistered = "This email or phone number is already registered.";
}
