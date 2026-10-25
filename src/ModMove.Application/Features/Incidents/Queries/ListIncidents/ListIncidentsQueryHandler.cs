using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Incidents.Queries.ListIncidents;

internal sealed class ListIncidentsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<ListIncidentsQuery, PagedResult<IncidentListItemResponse>>
{
    // A null parameter means "no filter". The filters are one constant so the page and the fallback count agree.
    private const string From = """
        FROM in_transit_incidents i
        JOIN shipping_orders o ON o.id = i.order_id
        JOIN users reporter ON reporter.id = i.reported_by
        LEFT JOIN users handler ON handler.id = i.handled_by
        WHERE (@Status IS NULL OR i.resolution_status = @Status)
          AND (@IncidentType IS NULL OR i.incident_type = @IncidentType)
          AND (@SearchPattern IS NULL OR o.order_code LIKE @SearchPattern)
        """;

    // COUNT(*) OVER() gives the total in the same round trip as the page.
    private const string PageSql = """
        SELECT i.id AS Id, i.order_id AS OrderId, o.order_code AS OrderCode, o.order_status AS OrderStatus,
               i.incident_type AS IncidentType, i.description AS Description,
               i.reported_by AS ReportedById, reporter.full_name AS ReporterName, reporter.role AS ReporterRole,
               i.resolution_status AS ResolutionStatus, i.handled_by AS HandledById, handler.full_name AS HandledByName,
               i.created_at AS CreatedAt, COUNT(*) OVER() AS TotalCount
        """ + "\n" + From + "\n" + """
        ORDER BY CASE i.resolution_status WHEN 'Open' THEN 0 WHEN 'InProgress' THEN 1 ELSE 2 END,
                 i.created_at, i.id
        LIMIT @PageSize OFFSET @Offset
        """;

    private const string CountSql = "SELECT COUNT(*)\n" + From;

    public async Task<PagedResult<IncidentListItemResponse>> Handle(ListIncidentsQuery request, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            Status = request.Status?.ToString(),
            IncidentType = request.IncidentType?.ToString(),
            SearchPattern = SqlHelpers.LikeContains(request.Search),
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        using var connection = connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<Row>(new CommandDefinition(PageSql, parameters, cancellationToken: cancellationToken))).AsList();

        // A page past the end has no row to carry the total, so ask for it.
        var total = rows.Count > 0
            ? rows[0].TotalCount
            : request.Page > 1
                ? await connection.QueryFirstOrDefaultAsync<int>(new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken))
                : 0;

        var items = rows.Select(r => new IncidentListItemResponse(
            r.Id, r.OrderId, r.OrderCode, r.OrderStatus, r.IncidentType, r.Description,
            r.ReportedById, r.ReporterName, r.ReporterRole, r.ResolutionStatus,
            r.HandledById, r.HandledByName, SqlHelpers.ToUtc(r.CreatedAt))).ToList();

        return new PagedResult<IncidentListItemResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed record Row(
        long Id, long OrderId, string OrderCode, string OrderStatus, string IncidentType, string Description,
        long ReportedById, string ReporterName, string ReporterRole, string ResolutionStatus,
        long? HandledById, string? HandledByName, DateTime CreatedAt, int TotalCount);
}
