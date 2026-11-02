using FluentValidation;

namespace ModMove.Application.Features.Refunds.Commands.ApproveRefund;

internal sealed class ApproveRefundCommandValidator : AbstractValidator<ApproveRefundCommand>
{
    public ApproveRefundCommandValidator()
    {
        RuleFor(x => x.RefundId).GreaterThan(0);
        RuleFor(x => x.Note)
            .MaximumLength(CustomerRefundRules.MaxTextLength)
            .When(x => x.Note is not null);
    }
}

