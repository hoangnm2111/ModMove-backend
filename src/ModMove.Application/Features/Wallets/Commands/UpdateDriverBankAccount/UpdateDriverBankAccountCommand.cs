using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Commands.UpdateDriverBankAccount;

/// <summary>UC-DRV-22: saves or updates the beneficiary bank account on the driver's profile for manual earnings payouts.</summary>
public sealed record UpdateDriverBankAccountCommand(
    string BankName,
    string BankAccountNumber,
    string BankAccountHolder) : ICommand;

