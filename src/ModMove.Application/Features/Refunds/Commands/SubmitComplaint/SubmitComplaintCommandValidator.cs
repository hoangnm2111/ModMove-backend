using FluentValidation;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Application.Features.Refunds.Commands.SubmitComplaint;

internal sealed class SubmitComplaintCommandValidator : AbstractValidator<SubmitComplaintCommand>
{
    public SubmitComplaintCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(CustomerRefundRules.MaxTextLength);
        RuleFor(x => x.EvidenceUrls).ValidEvidence(storage);
        RuleFor(x => x.RequestedAmount).ValidAmount();
    }
}
