using FluentValidation;

namespace ModMove.Application.Features.Refunds.Commands.ProposeComplaintResolution;

internal sealed class ProposeComplaintResolutionCommandValidator : AbstractValidator<ProposeComplaintResolutionCommand>
{
    public ProposeComplaintResolutionCommandValidator()
    {
        RuleFor(x => x.ComplaintId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
