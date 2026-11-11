using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverWithdrawals;

/// <summary>UC-DRV-22: paginated list of the signed-in driver's earnings withdrawal requests, newest first.</summary>
public sealed record GetDriverWithdrawalsQuery(
    int Page = 1,
    int PageSize = 20,
    WalletTransactionStatus? Status = null) : IQuery<PagedResult<DriverWithdrawalResponse>>;

public sealed record DriverWithdrawalResponse(
    long TransactionId,
    decimal Amount,
    decimal EarningsAmount,
    decimal DepositAmount,
    decimal BalanceAfter,
    string TransactionStatus,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountHolder,
    string? ReferenceCode,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset CreatedAt);

