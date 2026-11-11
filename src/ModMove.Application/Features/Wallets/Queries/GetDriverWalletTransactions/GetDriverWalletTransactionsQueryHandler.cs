using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverWalletTransactions;

internal sealed class GetDriverWalletTransactionsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser)
    : IQueryHandler<GetDriverWalletTransactionsQuery, PagedResult<DriverWalletTransactionResponse>>
{
    private const string FromSql = """
        FROM wallet_transactions wt
        JOIN driver_wallets w ON w.id = wt.wallet_id
        LEFT JOIN shipping_orders o ON o.id = wt.order_id
        WHERE w.driver_profile_id = @DriverProfileId
          AND (@Type IS NULL OR wt.transaction_type = @Type)
          AND (@Status IS NULL OR wt.transaction_status = @Status)
        """;

    private const string CountSql = $"SELECT COUNT(1) {FromSql}";

    private const string ItemsSql = $"""
        SELECT wt.id AS Id,
               wt.transaction_type AS TransactionType,
               wt.amount AS Amount,
               wt.earnings_amount AS EarningsAmount,
               wt.deposit_amount AS DepositAmount,
               wt.balance_after AS BalanceAfter,
               wt.transaction_status AS TransactionStatus,
               wt.order_id AS OrderId,
               o.order_code AS OrderCode,
               wt.reference_code AS ReferenceCode,
               wt.processed_at AS ProcessedAt,
               wt.created_at AS CreatedAt
        {FromSql}
        ORDER BY wt.created_at DESC, wt.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<DriverWalletTransactionResponse>> Handle(
        GetDriverWalletTransactionsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var context = await DriverAccess.LoadApprovedReadContextAsync(connection, userId, cancellationToken);

        var parameters = new
        {
            DriverProfileId = context.DriverProfileId!.Value,
            Type = request.Type?.ToString(),
            Status = request.Status?.ToString(),
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));
        if (total == 0)
            return new PagedResult<DriverWalletTransactionResponse>([], request.Page, request.PageSize, 0);

        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new DriverWalletTransactionResponse(
            r.Id,
            r.TransactionType,
            r.Amount,
            r.EarningsAmount,
            r.DepositAmount,
            r.BalanceAfter,
            r.TransactionStatus,
            r.OrderId,
            r.OrderCode,
            r.ReferenceCode,
            SqlHelpers.ToUtc(r.ProcessedAt),
            SqlHelpers.ToUtc(r.CreatedAt))).ToList();

        return new PagedResult<DriverWalletTransactionResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public string TransactionType { get; init; } = "";
        public decimal Amount { get; init; }
        public decimal EarningsAmount { get; init; }
        public decimal DepositAmount { get; init; }
        public decimal BalanceAfter { get; init; }
        public string TransactionStatus { get; init; } = "";
        public long? OrderId { get; init; }
        public string? OrderCode { get; init; }
        public string? ReferenceCode { get; init; }
        public DateTime? ProcessedAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}

