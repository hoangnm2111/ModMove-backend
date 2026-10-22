using FluentValidation;

namespace ModMove.Application.Features.Users.Commands.UpdateFcmToken;

internal sealed class UpdateFcmTokenCommandValidator : AbstractValidator<UpdateFcmTokenCommand>
{
    public UpdateFcmTokenCommandValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(255);
}
