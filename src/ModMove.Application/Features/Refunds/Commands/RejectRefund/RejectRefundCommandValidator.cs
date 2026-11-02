using FluentValidation;

namespace ModMove.Application.Features.Refunds.Commands.RejectRefund;

internal sealed class RejectRefundCommandValidator : AbstractValidator<RejectRefundCommand>
{
    public RejectRefundCommandValidator()
    {
        RuleFor(x => x.RefundId).GreaterThan(0);
        RuleFor(x => x.Note)
            .MaximumLength(CustomerRefundRules.MaxTextLength)
            .When(x => x.Note is not null);
    }
}

