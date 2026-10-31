using FluentValidation;

namespace ModMove.Application.Features.Refunds.Commands.WithdrawRefund;

internal sealed class WithdrawRefundCommandValidator : AbstractValidator<WithdrawRefundCommand>
{
    public WithdrawRefundCommandValidator() => RuleFor(x => x.RefundId).GreaterThan(0);
}
