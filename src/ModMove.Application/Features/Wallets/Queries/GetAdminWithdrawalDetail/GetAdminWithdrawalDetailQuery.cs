using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Queries.GetAdminWithdrawalDetail;

/// <summary>
/// UC-ADM-07, Screen #39: Full detail of a driver earnings withdrawal request for Admin review and manual transfer.
/// </summary>
public sealed record GetAdminWithdrawalDetailQuery(long TransactionId) : IQuery<AdminWithdrawalDetailResponse>;

public sealed record AdminWithdrawalDetailResponse(
    long TransactionId,
    long WalletId,
    long DriverProfileId,
    long DriverUserId,
    string DriverName,
    string DriverPhone,
    string DriverEmail,
    decimal Amount,
    decimal BalanceAfter,
    decimal CurrentEarningsBalance,
    decimal CurrentDepositBalance,
    string WalletStatus,
    string TransactionStatus,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountHolder,
    string? ReferenceCode,
    long? ProcessedBy,
    string? ProcessedByName,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset CreatedAt);

