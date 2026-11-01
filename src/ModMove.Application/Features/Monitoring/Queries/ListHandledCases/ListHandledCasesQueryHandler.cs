using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Monitoring.Queries.ListHandledCases;

internal sealed class ListHandledCasesQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser) : IQueryHandler<ListHandledCasesQuery, PagedResult<HandledCaseItemResponse>>
{
    private const string AccountStatusSql = "SELECT account_status AS AccountStatus FROM users WHERE id = @DispatcherId";

    private const string UnionFrom = """
        FROM (
            SELECT 'Incident' AS CaseKind,
                   i.id AS CaseId,
                   i.order_id AS OrderId,
                   o.order_code AS OrderCode,
                   i.incident_type AS SubType,
                   i.resolution_status AS Status,
                   i.resolution_decision AS Decision,
                   i.resolution_note AS Note,
                   CAST(NULL AS DECIMAL(15,2)) AS RequestedAmount,
                   CAST(NULL AS DECIMAL(15,2)) AS ProposedAmount,
                   COALESCE(i.resolved_at, i.updated_at, i.created_at) AS HandledAt
            FROM in_transit_incidents i
            JOIN shipping_orders o ON o.id = i.order_id
            WHERE i.handled_by = @DispatcherId
              AND @IncludeIncidents = 1

            UNION ALL

            SELECT 'Refund' AS CaseKind,
                   r.id AS CaseId,
                   r.order_id AS OrderId,
                   o.order_code AS OrderCode,
                   r.source_type AS SubType,
                   r.refund_status AS Status,
                   NULL AS Decision,
                   r.proposal_reason AS Note,
                   r.requested_amount AS RequestedAmount,
                   r.proposed_amount AS ProposedAmount,
                   COALESCE(r.decided_at, r.updated_at, r.created_at) AS HandledAt
            FROM refunds r
            JOIN shipping_orders o ON o.id = r.order_id
            WHERE r.proposed_by = @DispatcherId
              AND @IncludeRefunds = 1
              AND (@RefundSourceType IS NULL OR r.source_type = @RefundSourceType)
        ) cases
        """;

    private const string PageSql = """
        SELECT cases.CaseKind,
               cases.CaseId,
               cases.OrderId,
               cases.OrderCode,
               cases.SubType,
               cases.Status,
               cases.Decision,
               cases.Note,
               cases.RequestedAmount,
               cases.ProposedAmount,
               cases.HandledAt,
               COUNT(*) OVER() AS TotalCount
        """ + "\n" + UnionFrom + "\n" + """
        ORDER BY cases.HandledAt DESC, cases.CaseId DESC
        LIMIT @PageSize OFFSET @Offset;
        """;

    private const string CountSql = "SELECT COUNT(*)\n" + UnionFrom;

    public async Task<PagedResult<HandledCaseItemResponse>> Handle(
        ListHandledCasesQuery request, CancellationToken cancellationToken)
    {
        var dispatcherId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();

        var accountStatus = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(AccountStatusSql, new { DispatcherId = dispatcherId }, cancellationToken: cancellationToken))
            ?? throw new UnauthorizedException("Sign in again.");

        if (!string.Equals(accountStatus, "Active", StringComparison.Ordinal))
            throw CurrentUserExtensions.InactiveAccount();

        var (includeIncidents, includeRefunds, refundSourceType) = request.Type switch
        {
            HandledCaseType.Incident => (1, 0, (string?)null),
            HandledCaseType.Complaint => (0, 1, "Complaint"),
            HandledCaseType.IncidentRefund => (0, 1, "Incident"),
            HandledCaseType.Refund => (0, 1, null),
            _ => (1, 1, null),
        };

        var parameters = new
        {
            DispatcherId = dispatcherId,
            IncludeIncidents = includeIncidents,
            IncludeRefunds = includeRefunds,
            RefundSourceType = refundSourceType,
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        var rows = (await connection.QueryAsync<Row>(
            new CommandDefinition(PageSql, parameters, cancellationToken: cancellationToken))).AsList();

        var total = rows.Count > 0
            ? rows[0].TotalCount
            : request.Page > 1
                ? await connection.QueryFirstOrDefaultAsync<int>(new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken))
                : 0;

        var items = rows.Select(r => new HandledCaseItemResponse(
            r.CaseKind, r.CaseId, r.OrderId, r.OrderCode, r.SubType, r.Status,
            r.Decision, r.Note, r.RequestedAmount, r.ProposedAmount, SqlHelpers.ToUtc(r.HandledAt))).ToList();

        return new PagedResult<HandledCaseItemResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed class Row
    {
        public string CaseKind { get; init; } = "";
        public long CaseId { get; init; }
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public string SubType { get; init; } = "";
        public string Status { get; init; } = "";
        public string? Decision { get; init; }
        public string? Note { get; init; }
        public decimal? RequestedAmount { get; init; }
        public decimal? ProposedAmount { get; init; }
        public DateTime HandledAt { get; init; }
        public int TotalCount { get; init; }
    }
}

