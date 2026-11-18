using FluentValidation;
using ModMove.Domain.Constants;

namespace ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;

public sealed class RejectDriverApplicationCommandValidator : AbstractValidator<RejectDriverApplicationCommand>
{
    public RejectDriverApplicationCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.Reasons is { Count: > 0 }) || (x.RejectedItems is { Count: > 0 }))
            .WithMessage("At least one rejection reason or rejected item must be provided.")
            .WithName("Reasons");

        RuleForEach(x => x.Reasons)
            .NotEmpty().WithMessage("Rejection reason cannot be empty.")
            .When(x => x.Reasons is not null);

        RuleForEach(x => x.RejectedItems)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.Key)
                    .Must(k => DriverApplicationItemKeys.Normalize(k) is not null)
                    .WithMessage("Invalid rejected item key.");
                item.RuleFor(i => i.Reason)
                    .NotEmpty().WithMessage("Rejection reason cannot be empty.")
                    .MaximumLength(500).WithMessage("Rejection reason must not exceed 500 characters.");
            })
            .When(x => x.RejectedItems is not null);
    }
}
