using FluentValidation;

namespace ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;

public sealed class RejectDriverApplicationCommandValidator : AbstractValidator<RejectDriverApplicationCommand>
{
    public RejectDriverApplicationCommandValidator()
    {
        RuleFor(x => x.Reasons)
            .NotEmpty().WithMessage("At least one rejection reason must be provided.");

        RuleForEach(x => x.Reasons)
            .NotEmpty().WithMessage("Rejection reason cannot be empty.");
    }
}
