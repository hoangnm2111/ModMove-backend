using FluentValidation;

namespace ModMove.Application.Features.Users.Commands.UpdateUserStatus;

internal sealed class UpdateUserStatusCommandValidator : AbstractValidator<UpdateUserStatusCommand>
{
    public UpdateUserStatusCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);

        RuleFor(x => x.NewStatus).IsInEnum()
            .WithMessage("Invalid account status.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required when updating account status.")
            .MaximumLength(500);
    }
}

