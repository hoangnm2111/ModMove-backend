using FluentValidation;

namespace ModMove.Application.Features.Refunds.Commands.MarkRefundPayoutFailed;

internal sealed class MarkRefundPayoutFailedCommandValidator : AbstractValidator<MarkRefundPayoutFailedCommand>
{
    public const int MaxReasonLength = 1000;

    public MarkRefundPayoutFailedCommandValidator()
    {
        RuleFor(x => x.RefundId).GreaterThan(0);
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(MaxReasonLength);
    }
}

