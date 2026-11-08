using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Commands.RequestWithdrawal;

/// <summary>
/// UC-DRV-22, DECISIONS 5.1, GB-32: requests an earnings withdrawal with no minimum amount.
/// Any negative deposit debt is paid from earnings first (<c>CommissionDebtPaid</c>), and the requested amount is held
/// as <c>Pending</c> until an Administrator transfers it manually (GB-27).
/// Bank details may either be pre-saved via <c>PUT api/driver/wallet/bank-account</c> or supplied inline here.
/// </summary>
public sealed record RequestWithdrawalCommand(
    decimal Amount,
    string? BankName = null,
    string? BankAccountNumber = null,
    string? BankAccountHolder = null) : ICommand<WithdrawalCreatedResponse>;

public sealed record WithdrawalCreatedResponse(
    long TransactionId,
    decimal Amount,
    decimal DebtDeducted,
    decimal EarningsBalanceAfter,
    decimal DepositBalanceAfter,
    string TransactionStatus);

