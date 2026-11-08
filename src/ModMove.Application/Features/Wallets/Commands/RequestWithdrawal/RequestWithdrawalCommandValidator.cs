using System.Text.RegularExpressions;
using FluentValidation;

namespace ModMove.Application.Features.Wallets.Commands.RequestWithdrawal;

internal sealed class RequestWithdrawalCommandValidator : AbstractValidator<RequestWithdrawalCommand>
{
    public RequestWithdrawalCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .Must(a => decimal.Truncate(a) == a)
            .WithMessage("Withdrawal amount must be a whole VND amount.");

        When(HasAnyInlineBankField, () =>
        {
            RuleFor(x => x.BankName)
                .NotEmpty()
                .Must(v => v is null || v.Trim().Length <= 100)
                .WithMessage("Bank name must not exceed 100 characters.");
            RuleFor(x => x.BankAccountNumber)
                .NotEmpty()
                .Must(v => v is not null && Regex.IsMatch(v.Trim(), "^[0-9A-Za-z]{6,30}$"))
                .WithMessage("Bank account number must be 6 to 30 alphanumeric characters.");
            RuleFor(x => x.BankAccountHolder)
                .NotEmpty()
                .Must(v => v is null || v.Trim().Length <= 100)
                .WithMessage("Bank account holder must not exceed 100 characters.");
        });
    }

    private static bool HasAnyInlineBankField(RequestWithdrawalCommand x) =>
        x.BankName is not null || x.BankAccountNumber is not null || x.BankAccountHolder is not null;
}
