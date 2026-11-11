using FluentValidation;

namespace ModMove.Application.Features.Refunds.Commands.SetRefundPayoutAccount;

internal sealed class SetRefundPayoutAccountCommandValidator : AbstractValidator<SetRefundPayoutAccountCommand>
{
    public SetRefundPayoutAccountCommandValidator()
    {
        RuleFor(x => x.RefundId).GreaterThan(0);
        RuleFor(x => x.PayoutAccount).NotNull().ValidPayoutAccount();
    }
}
