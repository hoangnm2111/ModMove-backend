using FluentValidation;

namespace ModMove.Application.Features.Auth.Commands.ResendOtp;

internal sealed class ResendOtpCommandValidator : AbstractValidator<ResendOtpCommand>
{
    public ResendOtpCommandValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Purpose).IsInEnum();
    }
}

