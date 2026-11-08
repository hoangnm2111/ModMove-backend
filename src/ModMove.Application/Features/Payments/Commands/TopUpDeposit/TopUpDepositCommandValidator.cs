using FluentValidation;
using ModMove.Domain.Constants;

namespace ModMove.Application.Features.Payments.Commands.TopUpDeposit;

internal sealed class TopUpDepositCommandValidator : AbstractValidator<TopUpDepositCommand>
{
    public TopUpDepositCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(BusinessRules.MinDepositTopUp)
            .Must(amount => amount == decimal.Truncate(amount))
            .WithMessage("Deposit top-up amount must be a whole VND number.");

        RuleFor(x => x.Platform).IsInEnum();
    }
}

