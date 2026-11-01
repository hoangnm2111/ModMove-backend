using FluentValidation;

namespace ModMove.Application.Features.Refunds.Commands.ProposeComplaintResolution;

internal sealed class ProposeComplaintResolutionCommandValidator : AbstractValidator<ProposeComplaintResolutionCommand>
{
    public ProposeComplaintResolutionCommandValidator()
    {
        RuleFor(x => x.ComplaintId).GreaterThan(0);
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(CustomerRefundRules.MaxAmount)
            .Must(amount => amount == decimal.Truncate(amount))
            .WithMessage("The amount must be a whole number of VND.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(CustomerRefundRules.MaxTextLength);
    }
}
