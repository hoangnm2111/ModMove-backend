using FluentValidation;

namespace ModMove.Application.Features.Orders.Commands.ReviewPickupEvidence;

internal sealed class ReviewPickupEvidenceCommandValidator : AbstractValidator<ReviewPickupEvidenceCommand>
{
    public ReviewPickupEvidenceCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().When(x => !x.Approve).WithMessage("Say why the photos are rejected.");
        RuleFor(x => x.Reason).MaximumLength(500); // proof_of_deliveries.rejection_reason
    }
}
