using FluentValidation;

namespace ModMove.Application.Features.Auth.Commands.Logout;

internal sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() => RuleFor(x => x.FcmToken).MaximumLength(255); // users.fcm_token
}
