using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Wallets.Queries.GetAdminWithdrawals;

internal sealed class GetAdminWithdrawalsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser)
    : IQueryHandler<GetAdminWithdrawalsQuery, PagedResult<AdminWithdrawalSummary>>
{
    public async Task<PagedResult<AdminWithdrawalSummary>> Handle(GetAdminWithdrawalsQuery request, CancellationToken cancellationToken)
    {
        _ = currentUser.UserId ?? throw new UnauthorizedException();

        var conditions = new List<string> { "wt.transaction_type = 'Withdrawal'" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<WalletTransactionStatus>(request.Status, ignoreCase: true, out var parsedStatus))
        {
            conditions.Add("wt.transaction_status = @Status");
            parameters.Add("Status", parsedStatus.ToString());
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            conditions.Add("(u.full_name LIKE @Search OR u.phone_number LIKE @Search OR dp.bank_account_number LIKE @Search OR wt.reference_code LIKE @Search)");
            parameters.Add("Search", SqlHelpers.LikeContains(request.Search));
        }

        var whereSql = $"WHERE {string.Join(" AND ", conditions)}";

        var countSql = $"""
            SELECT COUNT(*)
            FROM wallet_transactions wt
            INNER JOIN driver_wallets w ON w.id = wt.wallet_id
            INNER JOIN driver_profiles dp ON dp.id = w.driver_profile_id
            INNER JOIN users u ON u.id = dp.user_id
            {whereSql};
            """;

        var offset = (request.Page - 1) * request.PageSize;
        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", offset);

        var itemsSql = $"""
            SELECT
                wt.id AS TransactionId,
                wt.wallet_id AS WalletId,
                w.driver_profile_id AS DriverProfileId,
                dp.user_id AS DriverUserId,
                u.full_name AS DriverName,
                u.phone_number AS DriverPhone,
                wt.amount AS Amount,
                wt.earnings_amount AS EarningsAmount,
                wt.deposit_amount AS DepositAmount,
                wt.balance_after AS BalanceAfter,
                wt.transaction_status AS TransactionStatus,
                dp.bank_name AS BankName,
                dp.bank_account_number AS BankAccountNumber,
                dp.bank_account_holder AS BankAccountHolder,
                wt.reference_code AS ReferenceCode,
                wt.processed_by AS ProcessedBy,
                wt.processed_at AS ProcessedAt,
                wt.created_at AS CreatedAt
            FROM wallet_transactions wt
            INNER JOIN driver_wallets w ON w.id = wt.wallet_id
            INNER JOIN driver_profiles dp ON dp.id = w.driver_profile_id
            INNER JOIN users u ON u.id = dp.user_id
            {whereSql}
            ORDER BY
                CASE wt.transaction_status
                    WHEN 'Pending' THEN 0
                    WHEN 'Completed' THEN 1
                    ELSE 2
                END,
                wt.created_at DESC,
                wt.id DESC
            LIMIT @PageSize OFFSET @Offset;
            """;

        using var conn = connectionFactory.CreateConnection();

        var totalCount = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        if (totalCount == 0)
            return new PagedResult<AdminWithdrawalSummary>([], request.Page, request.PageSize, 0);

        var rows = await conn.QueryAsync<Row>(
            new CommandDefinition(itemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new AdminWithdrawalSummary(
            r.TransactionId, r.WalletId, r.DriverProfileId, r.DriverUserId,
            r.DriverName, r.DriverPhone, r.Amount, r.EarningsAmount, r.DepositAmount,
            r.BalanceAfter, r.TransactionStatus,
            r.BankName, r.BankAccountNumber, r.BankAccountHolder,
            r.ReferenceCode, r.ProcessedBy,
            SqlHelpers.ToUtc(r.ProcessedAt), SqlHelpers.ToUtc(r.CreatedAt))).ToList();

        return new PagedResult<AdminWithdrawalSummary>(items, request.Page, request.PageSize, totalCount);
    }

    private sealed class Row
    {
        public long TransactionId { get; init; }
        public long WalletId { get; init; }
        public long DriverProfileId { get; init; }
        public long DriverUserId { get; init; }
        public string DriverName { get; init; } = "";
        public string DriverPhone { get; init; } = "";
        public decimal Amount { get; init; }
        public decimal EarningsAmount { get; init; }
        public decimal DepositAmount { get; init; }
        public decimal BalanceAfter { get; init; }
        public string TransactionStatus { get; init; } = "";
        public string? BankName { get; init; }
        public string? BankAccountNumber { get; init; }
        public string? BankAccountHolder { get; init; }
        public string? ReferenceCode { get; init; }
        public long? ProcessedBy { get; init; }
        public DateTime? ProcessedAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
