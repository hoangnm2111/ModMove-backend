using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Queries.GetAdminWithdrawalDetail;

internal sealed class GetAdminWithdrawalDetailQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser)
    : IQueryHandler<GetAdminWithdrawalDetailQuery, AdminWithdrawalDetailResponse>
{
    private const string Sql = """
        SELECT
            wt.id AS TransactionId,
            wt.wallet_id AS WalletId,
            w.driver_profile_id AS DriverProfileId,
            dp.user_id AS DriverUserId,
            u.full_name AS DriverName,
            u.phone_number AS DriverPhone,
            u.email AS DriverEmail,
            wt.amount AS Amount,
            wt.earnings_amount AS EarningsAmount,
            wt.deposit_amount AS DepositAmount,
            wt.balance_after AS BalanceAfter,
            w.earnings_balance AS CurrentEarningsBalance,
            w.deposit_balance AS CurrentDepositBalance,
            w.wallet_status AS WalletStatus,
            wt.transaction_status AS TransactionStatus,
            dp.bank_name AS BankName,
            dp.bank_account_number AS BankAccountNumber,
            dp.bank_account_holder AS BankAccountHolder,
            wt.reference_code AS ReferenceCode,
            wt.processed_by AS ProcessedBy,
            admin_user.full_name AS ProcessedByName,
            wt.processed_at AS ProcessedAt,
            wt.created_at AS CreatedAt
        FROM wallet_transactions wt
        INNER JOIN driver_wallets w ON w.id = wt.wallet_id
        INNER JOIN driver_profiles dp ON dp.id = w.driver_profile_id
        INNER JOIN users u ON u.id = dp.user_id
        LEFT JOIN users admin_user ON admin_user.id = wt.processed_by
        WHERE wt.id = @TransactionId
          AND wt.transaction_type = 'Withdrawal';
        """;

    public async Task<AdminWithdrawalDetailResponse> Handle(GetAdminWithdrawalDetailQuery request, CancellationToken cancellationToken)
    {
        _ = currentUser.UserId ?? throw new UnauthorizedException();

        using var conn = connectionFactory.CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(Sql, new { request.TransactionId }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException("WalletTransaction", request.TransactionId);

        return new AdminWithdrawalDetailResponse(
            row.TransactionId, row.WalletId, row.DriverProfileId, row.DriverUserId,
            row.DriverName, row.DriverPhone, row.DriverEmail,
            row.Amount, row.EarningsAmount, row.DepositAmount, row.BalanceAfter,
            row.CurrentEarningsBalance, row.CurrentDepositBalance, row.WalletStatus,
            row.TransactionStatus,
            row.BankName, row.BankAccountNumber, row.BankAccountHolder,
            row.ReferenceCode, row.ProcessedBy, row.ProcessedByName,
            SqlHelpers.ToUtc(row.ProcessedAt), SqlHelpers.ToUtc(row.CreatedAt));
    }

    private sealed class Row
    {
        public long TransactionId { get; init; }
        public long WalletId { get; init; }
        public long DriverProfileId { get; init; }
        public long DriverUserId { get; init; }
        public string DriverName { get; init; } = "";
        public string DriverPhone { get; init; } = "";
        public string DriverEmail { get; init; } = "";
        public decimal Amount { get; init; }
        public decimal EarningsAmount { get; init; }
        public decimal DepositAmount { get; init; }
        public decimal BalanceAfter { get; init; }
        public decimal CurrentEarningsBalance { get; init; }
        public decimal CurrentDepositBalance { get; init; }
        public string WalletStatus { get; init; } = "";
        public string TransactionStatus { get; init; } = "";
        public string? BankName { get; init; }
        public string? BankAccountNumber { get; init; }
        public string? BankAccountHolder { get; init; }
        public string? ReferenceCode { get; init; }
        public long? ProcessedBy { get; init; }
        public string? ProcessedByName { get; init; }
        public DateTime? ProcessedAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
