using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverWallet;

/// <summary>
/// UC-DRV-20, GB-16, GB-32: overview of the signed-in driver's deposit &amp; earnings wallets,
/// wallet total, missing deposit amount, maximum withdrawable amount, order eligibility, and linked bank account.
/// </summary>
public sealed record GetDriverWalletQuery : IQuery<DriverWalletResponse>;

public sealed record DriverWalletResponse(
    long WalletId,
    string WalletStatus,
    decimal DepositBalance,
    decimal EarningsBalance,
    decimal WalletTotal,
    decimal MissingDepositAmount,
    decimal MaxWithdrawableAmount,
    decimal RequiredDeposit,
    bool CanReceiveOrders,
    bool HasActiveOrder,
    bool HasPendingWithdrawal,
    decimal PendingWithdrawalAmount,
    DriverBankAccountResponse? BankAccount);

public sealed record DriverBankAccountResponse(
    string BankName,
    string BankAccountNumber,
    string BankAccountHolder);

