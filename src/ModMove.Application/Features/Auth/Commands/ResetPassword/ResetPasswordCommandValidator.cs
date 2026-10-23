using FluentValidation;

namespace ModMove.Application.Features.Auth.Commands.ResetPassword;

internal sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$");
        RuleFor(x => x.NewPassword).ValidPassword();
    }
}
