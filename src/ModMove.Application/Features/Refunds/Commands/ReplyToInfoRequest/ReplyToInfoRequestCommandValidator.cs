using FluentValidation;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Application.Features.Refunds.Commands.ReplyToInfoRequest;

internal sealed class ReplyToInfoRequestCommandValidator : AbstractValidator<ReplyToInfoRequestCommand>
{
    public ReplyToInfoRequestCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x.RefundId).GreaterThan(0);
        RuleFor(x => x.Reply).NotEmpty().MaximumLength(CustomerRefundRules.MaxTextLength);
        RuleFor(x => x.PhotoUrls).ValidEvidence(storage);
    }
}
