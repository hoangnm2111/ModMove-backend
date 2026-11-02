using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Refunds.Queries.GetAdminRefunds;

internal sealed class GetAdminRefundsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetAdminRefundsQuery, PagedResult<AdminRefundDto>>
{
    private const string FromSql = """
        FROM refunds r
        JOIN shipping_orders o ON o.id = r.order_id
        JOIN users cust ON cust.id = o.customer_id
        LEFT JOIN users proposer ON proposer.id = r.proposed_by
        WHERE (@Status IS NULL OR r.refund_status = @Status)
          AND (@SourceType IS NULL OR r.source_type = @SourceType)
          AND (@SearchPattern IS NULL
               OR o.order_code LIKE @SearchPattern
               OR cust.full_name LIKE @SearchPattern
               OR cust.phone_number LIKE @SearchPattern)
        """;

    private const string CountSql = "SELECT COUNT(1)\n" + FromSql;

    private const string ItemsSql = """
        SELECT r.id AS Id, r.order_id AS OrderId, o.order_code AS OrderCode,
               cust.full_name AS CustomerName, cust.phone_number AS CustomerPhone,
               r.source_type AS SourceType, r.refund_status AS RefundStatus,
               r.requested_amount AS RequestedAmount, r.proposed_amount AS ProposedAmount,
               r.reason_code AS ReasonCode, r.reason AS Reason,
               r.proposal_reason AS ProposalReason, proposer.full_name AS ProposedByName,
               r.decision_note AS DecisionNote, r.bank_reference_code AS BankReferenceCode,
               r.decided_at AS DecidedAt, r.processed_at AS ProcessedAt, r.created_at AS CreatedAt
        """ + "\n" + FromSql + "\n" + """
        ORDER BY CASE
            WHEN r.refund_status = 'Requested' AND r.proposed_amount IS NOT NULL THEN 0
            WHEN r.refund_status = 'Approved' THEN 1
            WHEN r.refund_status = 'Requested' THEN 2
            ELSE 3
        END, r.created_at DESC, r.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<AdminRefundDto>> Handle(GetAdminRefundsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var parameters = new
        {
            Status = request.Status?.ToString(),
            SourceType = request.SourceType?.ToString(),
            SearchPattern = SqlHelpers.LikeContains(request.Search),
            PageSize = pageSize,
            Offset = (page - 1) * pageSize,
        };

        using var connection = connectionFactory.CreateConnection();
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new AdminRefundDto(
            r.Id, r.OrderId, r.OrderCode, r.CustomerName, r.CustomerPhone,
            r.SourceType, r.RefundStatus, r.RequestedAmount, r.ProposedAmount,
            r.ReasonCode, r.Reason, r.ProposalReason, r.ProposedByName,
            r.DecisionNote, r.BankReferenceCode,
            SqlHelpers.ToUtc(r.DecidedAt), SqlHelpers.ToUtc(r.ProcessedAt), SqlHelpers.ToUtc(r.CreatedAt))).ToList();

        return new PagedResult<AdminRefundDto>(items, page, pageSize, totalCount);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string CustomerPhone { get; init; } = "";
        public string SourceType { get; init; } = "";
        public string RefundStatus { get; init; } = "";
        public decimal RequestedAmount { get; init; }
        public decimal? ProposedAmount { get; init; }
        public string? ReasonCode { get; init; }
        public string Reason { get; init; } = "";
        public string? ProposalReason { get; init; }
        public string? ProposedByName { get; init; }
        public string? DecisionNote { get; init; }
        public string? BankReferenceCode { get; init; }
        public DateTime? DecidedAt { get; init; }
        public DateTime? ProcessedAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}

