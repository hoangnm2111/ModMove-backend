using System.Text.RegularExpressions;
using FluentValidation;

namespace ModMove.Application.Features.Wallets.Commands.UpdateDriverBankAccount;

internal sealed class UpdateDriverBankAccountCommandValidator : AbstractValidator<UpdateDriverBankAccountCommand>
{
    public UpdateDriverBankAccountCommandValidator()
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
    }
}
