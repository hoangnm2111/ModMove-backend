using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverWallet;

/// <summary>
/// UC-DRV-20, DECISIONS 5.1, GB-16: overview of the signed-in driver's deposit &amp; earnings wallets,
/// maximum withdrawable amount after negative deposit debt, COD eligibility, and linked bank account.
/// </summary>
public sealed record GetDriverWalletQuery : IQuery<DriverWalletResponse>;

public sealed record DriverWalletResponse(
    long WalletId,
    string WalletStatus,
    decimal DepositBalance,
    decimal EarningsBalance,
    decimal CommissionDebt,
    decimal MaxWithdrawableAmount,
    decimal MinCodDeposit,
    bool CanReceiveCod,
    bool HasPendingWithdrawal,
    decimal PendingWithdrawalAmount,
    DriverBankAccountResponse? BankAccount);

public sealed record DriverBankAccountResponse(
    string BankName,
    string BankAccountNumber,
    string BankAccountHolder);

