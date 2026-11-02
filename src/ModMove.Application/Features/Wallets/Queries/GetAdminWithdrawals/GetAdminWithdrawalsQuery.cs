using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Wallets.Queries.GetAdminWithdrawals;

/// <summary>
/// UC-ADM-07, Screen #39: Admin lists driver earnings withdrawal requests with pagination and filters.
/// </summary>
public sealed record GetAdminWithdrawalsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Search = null) : IQuery<PagedResult<AdminWithdrawalSummary>>;

public sealed record AdminWithdrawalSummary(
    long TransactionId,
    long WalletId,
    long DriverProfileId,
    long DriverUserId,
    string DriverName,
    string DriverPhone,
    decimal Amount,
    decimal BalanceAfter,
    string TransactionStatus,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountHolder,
    string? ReferenceCode,
    long? ProcessedBy,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset CreatedAt);
