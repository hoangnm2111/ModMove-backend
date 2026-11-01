using FluentValidation;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Application.Features.Refunds.Commands.RequestIncidentRefund;

internal sealed class RequestIncidentRefundCommandValidator : AbstractValidator<RequestIncidentRefundCommand>
{
    public RequestIncidentRefundCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x.IncidentId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(CustomerRefundRules.MaxTextLength);
        RuleFor(x => x.EvidenceUrls).ValidEvidence(storage);
        RuleFor(x => x.RequestedAmount).ValidAmount();
    }
}
