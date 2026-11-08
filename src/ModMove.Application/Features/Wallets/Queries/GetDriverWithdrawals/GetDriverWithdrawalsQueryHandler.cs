using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverWithdrawals;

internal sealed class GetDriverWithdrawalsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser)
    : IQueryHandler<GetDriverWithdrawalsQuery, PagedResult<DriverWithdrawalResponse>>
{
    private const string FromSql = """
        FROM wallet_transactions wt
        JOIN driver_wallets w ON w.id = wt.wallet_id
        WHERE w.driver_profile_id = @DriverProfileId
          AND wt.transaction_type = 'WithdrawEarnings'
          AND (@Status IS NULL OR wt.transaction_status = @Status)
        """;

    private const string CountSql = $"SELECT COUNT(1) {FromSql}";

    private const string ItemsSql = $"""
        SELECT wt.id AS TransactionId,
               wt.amount AS Amount,
               wt.balance_after AS BalanceAfter,
               wt.transaction_status AS TransactionStatus,
               wt.reference_code AS ReferenceCode,
               wt.processed_at AS ProcessedAt,
               wt.created_at AS CreatedAt
        {FromSql}
        ORDER BY wt.created_at DESC, wt.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<DriverWithdrawalResponse>> Handle(
        GetDriverWithdrawalsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var context = await DriverAccess.LoadApprovedReadContextAsync(connection, userId, cancellationToken);

        var parameters = new
        {
            DriverProfileId = context.DriverProfileId!.Value,
            Status = request.Status?.ToString(),
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));
        if (total == 0)
            return new PagedResult<DriverWithdrawalResponse>([], request.Page, request.PageSize, 0);

        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new DriverWithdrawalResponse(
            r.TransactionId,
            r.Amount,
            r.BalanceAfter,
            r.TransactionStatus,
            context.BankName,
            context.BankAccountNumber,
            context.BankAccountHolder,
            r.ReferenceCode,
            SqlHelpers.ToUtc(r.ProcessedAt),
            SqlHelpers.ToUtc(r.CreatedAt))).ToList();

        return new PagedResult<DriverWithdrawalResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed class Row
    {
        public long TransactionId { get; init; }
        public decimal Amount { get; init; }
        public decimal BalanceAfter { get; init; }
        public string TransactionStatus { get; init; } = "";
        public string? ReferenceCode { get; init; }
        public DateTime? ProcessedAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}

