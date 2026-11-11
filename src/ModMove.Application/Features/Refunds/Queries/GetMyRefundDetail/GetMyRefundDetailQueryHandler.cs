using System.Text.Json;
using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Orders.Queries;

namespace ModMove.Application.Features.Refunds.Queries.GetMyRefundDetail;

internal sealed class GetMyRefundDetailQueryHandler(ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser)
    : IQueryHandler<GetMyRefundDetailQuery, MyRefundDetailResponse>
{
    private const string Sql = $"""
        SELECT {RefundRow.Columns}
        {RefundRow.FromCustomerRefunds}
          AND r.id = @RefundId
        """;

    private const string SuggestionSql = """
        SELECT p.payer_bank_bin AS BankCode, b.name AS BankName,
               p.payer_account_number AS AccountNumber, p.payer_account_name AS AccountHolder
        FROM payment_transactions p
        JOIN master_data_items b ON b.category = 'BANK' AND b.code = p.payer_bank_bin AND b.is_active = 1
        WHERE p.order_id = @OrderId AND p.payment_method = 'PayOS' AND p.transaction_status = 'Paid'
          AND p.payer_account_number IS NOT NULL
        ORDER BY p.paid_at DESC, p.id DESC
        LIMIT 1
        """;

    public async Task<MyRefundDetailResponse> Handle(GetMyRefundDetailQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        await CustomerAccess.EnsureActiveAsync(connection, customerId, cancellationToken);

        var r = await connection.QuerySingleOrDefaultAsync<RefundRow>(
            new CommandDefinition(Sql, new { CustomerId = customerId, request.RefundId }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException("Refund", request.RefundId);

        return new MyRefundDetailResponse(
            r.Id, r.OrderId, r.OrderCode, r.IncidentId, r.SourceType, r.RefundStatus, r.Reason, r.ReasonCode,
            r.EvidenceUrls is null ? [] : JsonSerializer.Deserialize<List<string>>(r.EvidenceUrls) ?? [],
            r.RequestedAmount, r.ApprovedAmount, r.DecisionReason, r.InfoRequestNote, r.CustomerReply, r.WaitingForReply,
            CustomerAccess.Utc(r.CreatedAt),
            r.DecidedAt is { } decided ? CustomerAccess.Utc(decided) : null,
            r.ProcessedAt is { } processed ? CustomerAccess.Utc(processed) : null,
            r.PayoutAccount, r.NeedsPayoutAccount, r.PayoutFailureReason,
            r.RefundStatus == "Processed" ? r.BankReferenceCode : null,
            r.NeedsPayoutAccount ? await ReadSuggestionAsync(connection, r.OrderId, cancellationToken) : null);
    }

    /// <summary>
    /// Q3: the account the customer paid from through PayOS, offered to confirm. Read only while the refund still needs an
    /// account, joined to the BANK list so a bank that is not offered is not suggested, and returned masked: the client
    /// never gets the full number and never sends it back (the server copies it again on confirm).
    /// </summary>
    private static async Task<PayoutAccountView?> ReadSuggestionAsync(
        System.Data.IDbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        var payer = await connection.QueryFirstOrDefaultAsync<PayerRow>(
            new CommandDefinition(SuggestionSql, new { OrderId = orderId }, cancellationToken: cancellationToken));
        if (payer is null)
            return null;

        // The same checks the server runs when the customer confirms, so a suggestion is never one that confirm would refuse.
        var holder = PayoutAccountResolver.NormalizePayerName(payer.AccountHolder);
        if (payer.AccountNumber is not { } number || !IsValidNumber(number) || holder.Length < 2)
            return null;

        return new PayoutAccountView(payer.BankCode, payer.BankName, PayoutAccountView.Mask(number), holder);
    }

    private static bool IsValidNumber(string number) =>
        number.Length is >= 6 and <= 19 && number.All(c => c is >= '0' and <= '9');

    private sealed class PayerRow
    {
        public string BankCode { get; init; } = "";
        public string BankName { get; init; } = "";
        public string? AccountNumber { get; init; }
        public string? AccountHolder { get; init; }
    }
}
