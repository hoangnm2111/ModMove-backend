using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Refunds.Queries.ListComplaints;

internal sealed class ListComplaintsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<ListComplaintsQuery, PagedResult<ComplaintListItemResponse>>
{
    private const string StageExpression = """
        CASE
            WHEN r.refund_status != 'Requested' THEN 'Closed'
            WHEN r.proposed_by IS NOT NULL THEN 'ProposedToAdmin'
            WHEN r.customer_reply IS NOT NULL THEN 'CustomerReplied'
            WHEN r.info_request_note IS NOT NULL THEN 'WaitingForCustomer'
            ELSE 'NeedsReview'
        END
        """;

    private const string From = """
        FROM refunds r
        JOIN shipping_orders o ON o.id = r.order_id
        JOIN users cust ON cust.id = o.customer_id
        WHERE r.source_type = 'Complaint'
          AND (@Stage IS NULL OR (
        """ + StageExpression + """
          ) = @Stage)
          AND (@SearchPattern IS NULL OR o.order_code LIKE @SearchPattern)
        """;

    private const string PageSql = """
        SELECT r.id AS Id, r.order_id AS OrderId, o.order_code AS OrderCode,
               cust.full_name AS CustomerName, r.reason AS Reason,
               r.requested_amount AS RequestedAmount, r.proposed_amount AS ProposedAmount,
        """ + "\n" + StageExpression + " AS Stage,\n" + """
               r.refund_status AS RefundStatus, o.delivered_at AS DeliveredAt,
               r.created_at AS CreatedAt, COUNT(*) OVER() AS TotalCount
        """ + "\n" + From + "\n" + """
        ORDER BY CASE
            WHEN r.refund_status = 'Requested' AND r.proposed_by IS NULL AND (r.info_request_note IS NULL OR r.customer_reply IS NOT NULL) THEN 0
            WHEN r.refund_status = 'Requested' AND r.info_request_note IS NOT NULL AND r.customer_reply IS NULL AND r.proposed_by IS NULL THEN 1
            WHEN r.refund_status = 'Requested' AND r.proposed_by IS NOT NULL THEN 2
            ELSE 3
        END, r.created_at, r.id
        LIMIT @PageSize OFFSET @Offset
        """;

    private const string CountSql = "SELECT COUNT(*)\n" + From;

    public async Task<PagedResult<ComplaintListItemResponse>> Handle(ListComplaintsQuery request, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            Stage = request.Stage?.ToString(),
            SearchPattern = SqlHelpers.LikeContains(request.Search),
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        using var connection = connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<Row>(new CommandDefinition(PageSql, parameters, cancellationToken: cancellationToken))).AsList();

        var total = rows.Count > 0
            ? rows[0].TotalCount
            : request.Page > 1
                ? await connection.QueryFirstOrDefaultAsync<int>(new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken))
                : 0;

        var items = rows.Select(r => new ComplaintListItemResponse(
            r.Id, r.OrderId, r.OrderCode, r.CustomerName, r.Reason,
            r.RequestedAmount, r.ProposedAmount, r.Stage, r.RefundStatus,
            SqlHelpers.ToUtc(r.DeliveredAt), SqlHelpers.ToUtc(r.CreatedAt))).ToList();

        return new PagedResult<ComplaintListItemResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string Reason { get; init; } = "";
        public decimal RequestedAmount { get; init; }
        public decimal? ProposedAmount { get; init; }
        public string Stage { get; init; } = "";
        public string RefundStatus { get; init; } = "";
        public DateTime? DeliveredAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public int TotalCount { get; init; }
    }
}
