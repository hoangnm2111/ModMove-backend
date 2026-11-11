using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverWalletTransactions;

/// <summary>UC-DRV-20: paginated transaction ledger of the signed-in driver's wallet, newest first.</summary>
public sealed record GetDriverWalletTransactionsQuery(
    int Page = 1,
    int PageSize = 20,
    WalletTransactionType? Type = null,
    WalletTransactionStatus? Status = null) : IQuery<PagedResult<DriverWalletTransactionResponse>>;

public sealed record DriverWalletTransactionResponse(
    long Id,
    string TransactionType,
    decimal Amount,
    decimal EarningsAmount,
    decimal DepositAmount,
    decimal BalanceAfter,
    string TransactionStatus,
    long? OrderId,
    string? OrderCode,
    string? ReferenceCode,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset CreatedAt);

