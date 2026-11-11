using FluentValidation;

namespace ModMove.Application.Features.Payments.Commands.TopUpDeposit;

internal sealed class TopUpDepositCommandValidator : AbstractValidator<TopUpDepositCommand>
{
    public TopUpDepositCommandValidator()
    {
        // GB-32, UC-DRV-21: any positive whole amount; the handler checks it covers the missing deposit (the suggested amount).
        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .Must(amount => amount == decimal.Truncate(amount))
            .WithMessage("Deposit top-up amount must be a whole VND number.");

        RuleFor(x => x.Platform).IsInEnum();
    }
}
