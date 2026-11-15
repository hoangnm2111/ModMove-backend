using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Orders;

namespace ModMove.Application.Features.Reports.Queries.ExportApprovedPayouts;

internal static class ApprovedPayoutsDataLoader
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateTimeOffset ToVietnamTime(DateTimeOffset utc) =>
        utc.ToOffset(VietnamOffset);

    private const string PayoutsSql = """
        SELECT
            payouts.PayoutReference,
            payouts.PayoutType,
            payouts.Role,
            payouts.BeneficiaryName,
            payouts.BeneficiaryAccountNumber,
            payouts.BeneficiaryBankName,
            payouts.BeneficiaryBankBin,
            payouts.Amount,
            payouts.TransferDescription,
            payouts.PhoneNumber,
            payouts.OrderCode,
            payouts.ApprovedAt,
            payouts.ApprovedByName
        FROM (
            -- 1. Yêu cầu rút tiền từ ví tài xế (Pending payout)
            SELECT
                CONCAT('WD-', wt.id) AS PayoutReference,
                'Rút thu nhập ví tài xế' AS PayoutType,
                'Tài xế (Driver)' AS Role,
                COALESCE(NULLIF(TRIM(dp.bank_account_holder), ''), u.full_name) AS BeneficiaryName,
                COALESCE(dp.bank_account_number, '') AS BeneficiaryAccountNumber,
                COALESCE(dp.bank_name, '') AS BeneficiaryBankName,
                mb.code AS BeneficiaryBankBin,
                wt.amount AS Amount,
                CONCAT('MODMOVE CHI RUT VI WD', wt.id) AS TransferDescription,
                u.phone_number AS PhoneNumber,
                '-' AS OrderCode,
                wt.created_at AS ApprovedAt,
                admin_u.full_name AS ApprovedByName
            FROM wallet_transactions wt
            INNER JOIN driver_wallets w ON w.id = wt.wallet_id
            INNER JOIN driver_profiles dp ON dp.id = w.driver_profile_id
            INNER JOIN users u ON u.id = dp.user_id
            LEFT JOIN users admin_u ON admin_u.id = wt.processed_by
            LEFT JOIN master_data_items mb ON mb.category = 'BANK' AND (mb.name = dp.bank_name OR mb.code = dp.bank_name)
            WHERE wt.transaction_type = 'Withdrawal'
              AND wt.transaction_status = 'Pending'
              AND (@IncludeDriver = 1)
              AND (@FromUtc IS NULL OR wt.created_at >= @FromUtc)
              AND (@ToUtc IS NULL OR wt.created_at < @ToUtc)

            UNION ALL

            -- 2. Yêu cầu hoàn tiền khách hàng (Approved refund)
            SELECT
                CONCAT('RF-', r.id) AS PayoutReference,
                CASE r.source_type
                    WHEN 'Cancellation' THEN 'Hoàn tiền hủy đơn'
                    WHEN 'Incident' THEN 'Bồi thường sự cố'
                    WHEN 'Complaint' THEN 'Giải quyết khiếu nại'
                    ELSE 'Hoàn tiền khác'
                END AS PayoutType,
                'Khách hàng (Customer)' AS Role,
                COALESCE(NULLIF(TRIM(r.payout_account_holder), ''), cu.full_name) AS BeneficiaryName,
                COALESCE(r.payout_account_number, '') AS BeneficiaryAccountNumber,
                COALESCE(r.payout_bank_name, '') AS BeneficiaryBankName,
                r.payout_bank_code AS BeneficiaryBankBin,
                COALESCE(r.proposed_amount, r.requested_amount) AS Amount,
                CONCAT('MODMOVE HOAN TIEN DON ', o.order_code, ' RF', r.id) AS TransferDescription,
                cu.phone_number AS PhoneNumber,
                o.order_code AS OrderCode,
                COALESCE(r.decided_at, r.created_at) AS ApprovedAt,
                admin_u.full_name AS ApprovedByName
            FROM refunds r
            INNER JOIN shipping_orders o ON o.id = r.order_id
            INNER JOIN users cu ON cu.id = o.customer_id
            LEFT JOIN users admin_u ON admin_u.id = r.approved_by
            WHERE r.refund_status = 'Approved'
              AND r.payout_account_number IS NOT NULL
              AND (@IncludeCustomer = 1)
              AND (@FromUtc IS NULL OR COALESCE(r.decided_at, r.created_at) >= @FromUtc)
              AND (@ToUtc IS NULL OR COALESCE(r.decided_at, r.created_at) < @ToUtc)
        ) payouts
        ORDER BY payouts.ApprovedAt DESC, payouts.PayoutReference DESC;
        """;

    public static async Task<IReadOnlyList<ApprovedPayoutItemDto>> LoadAsync(
        ISqlConnectionFactory connectionFactory,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? role,
        CancellationToken cancellationToken)
    {
        var includeDriver = string.IsNullOrWhiteSpace(role)
            || role.Equals("All", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Driver", StringComparison.OrdinalIgnoreCase);

        var includeCustomer = string.IsNullOrWhiteSpace(role)
            || role.Equals("All", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Customer", StringComparison.OrdinalIgnoreCase);

        var parameters = new
        {
            FromUtc = fromDate is { } from ? OrderCode.VietnamDayStartUtc(from).UtcDateTime : (DateTime?)null,
            ToUtc = toDate is { } to ? OrderCode.VietnamDayStartUtc(to.AddDays(1)).UtcDateTime : (DateTime?)null,
            IncludeDriver = includeDriver ? 1 : 0,
            IncludeCustomer = includeCustomer ? 1 : 0,
        };

        using var connection = connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<Row>(
            new CommandDefinition(PayoutsSql, parameters, cancellationToken: cancellationToken))).ToList();

        var result = new List<ApprovedPayoutItemDto>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            result.Add(new ApprovedPayoutItemDto
            {
                Index = i + 1,
                PayoutReference = r.PayoutReference,
                PayoutType = r.PayoutType,
                Role = r.Role,
                BeneficiaryName = r.BeneficiaryName.ToUpperInvariant(),
                BeneficiaryAccountNumber = r.BeneficiaryAccountNumber,
                BeneficiaryBankName = r.BeneficiaryBankName,
                BeneficiaryBankBin = r.BeneficiaryBankBin,
                Amount = r.Amount,
                TransferDescription = r.TransferDescription,
                PhoneNumber = r.PhoneNumber,
                OrderCode = r.OrderCode,
                ApprovedAt = SqlHelpers.ToUtc(r.ApprovedAt),
                ApprovedByName = r.ApprovedByName,
            });
        }

        return result;
    }

    private sealed class Row
    {
        public string PayoutReference { get; init; } = "";
        public string PayoutType { get; init; } = "";
        public string Role { get; init; } = "";
        public string BeneficiaryName { get; init; } = "";
        public string BeneficiaryAccountNumber { get; init; } = "";
        public string BeneficiaryBankName { get; init; } = "";
        public string? BeneficiaryBankBin { get; init; }
        public decimal Amount { get; init; }
        public string TransferDescription { get; init; } = "";
        public string PhoneNumber { get; init; } = "";
        public string? OrderCode { get; init; }
        public DateTime? ApprovedAt { get; init; }
        public string? ApprovedByName { get; init; }
    }
}
