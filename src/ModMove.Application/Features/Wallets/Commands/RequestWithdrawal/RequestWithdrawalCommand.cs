using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Commands.RequestWithdrawal;

/// <summary>
/// UC-DRV-22, GB-32: requests a wallet withdrawal with no minimum amount, up to the wallet total (earnings only while the driver
/// has an active order). The amount is taken from earnings first and from the deposit for the remainder, and held as
/// <c>Pending</c> until an Administrator transfers it manually (GB-27).
/// Bank details may either be pre-saved via <c>PUT api/driver/wallet/bank-account</c> or supplied inline here.
/// </summary>
public sealed record RequestWithdrawalCommand(
    decimal Amount,
    string? BankName = null,
    string? BankAccountNumber = null,
    string? BankAccountHolder = null) : ICommand<WithdrawalCreatedResponse>;

/// <param name="EarningsDeducted">The part held from EarningsBalance.</param>
/// <param name="DepositDeducted">The part held from DepositBalance (0 unless the amount was above the earnings).</param>
/// <param name="DepositBelowRequired">True when the deposit is now below REQUIRED_DEPOSIT, so no orders until it is restored (MSG69).</param>
public sealed record WithdrawalCreatedResponse(
    long TransactionId,
    decimal Amount,
    decimal EarningsDeducted,
    decimal DepositDeducted,
    decimal EarningsBalanceAfter,
    decimal DepositBalanceAfter,
    decimal WalletTotalAfter,
    bool DepositBelowRequired,
    string TransactionStatus);
