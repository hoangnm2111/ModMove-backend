using FluentValidation;

namespace ModMove.Application.Features.Auth.Commands.VerifyEmail;

internal sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$");
    }
}

