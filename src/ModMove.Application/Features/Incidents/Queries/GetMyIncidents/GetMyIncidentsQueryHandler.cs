using System.Text.Json;
using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Orders.Queries;

namespace ModMove.Application.Features.Incidents.Queries.GetMyIncidents;

internal sealed class GetMyIncidentsQueryHandler(ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser)
    : IQueryHandler<GetMyIncidentsQuery, PagedResult<MyIncidentResponse>>
{
    // Joined with the order so only incidents of the caller's orders come back (IDOR, C10).
    private const string From = """
        FROM in_transit_incidents i
        JOIN shipping_orders o ON o.id = i.order_id
        WHERE o.customer_id = @CustomerId
          AND (@OrderId IS NULL OR i.order_id = @OrderId)
          AND (@Status IS NULL OR i.resolution_status = @Status)
        """;

    private const string CountSql = $"SELECT COUNT(1) {From}";

    private const string ItemsSql = $"""
        SELECT i.id AS Id, i.order_id AS OrderId, o.order_code AS OrderCode, i.incident_type AS IncidentType,
               i.description AS Description, i.photo_urls AS PhotoUrls, i.reported_by AS ReportedBy,
               i.resolution_status AS ResolutionStatus, i.resolution_decision AS ResolutionDecision,
               i.resolution_note AS ResolutionNote, i.resolved_at AS ResolvedAt, i.created_at AS CreatedAt
        {From}
        ORDER BY i.created_at DESC, i.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<MyIncidentResponse>> Handle(GetMyIncidentsQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        await CustomerAccess.EnsureActiveAsync(connection, customerId, cancellationToken);

        var parameters = new
        {
            CustomerId = customerId,
            request.OrderId,
            Status = request.Status?.ToString(),
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new MyIncidentResponse(
            r.Id, r.OrderId, r.OrderCode, r.IncidentType, r.Description,
            r.PhotoUrls is null ? [] : JsonSerializer.Deserialize<List<string>>(r.PhotoUrls) ?? [],
            r.ReportedBy == customerId, r.ResolutionStatus, r.ResolutionDecision, r.ResolutionNote,
            CustomerAccess.Utc(r.CreatedAt), r.ResolvedAt is { } resolved ? CustomerAccess.Utc(resolved) : null)).ToList();
        return new PagedResult<MyIncidentResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public string IncidentType { get; init; } = "";
        public string Description { get; init; } = "";
        public string? PhotoUrls { get; init; }
        public long ReportedBy { get; init; }
        public string ResolutionStatus { get; init; } = "";
        public string? ResolutionDecision { get; init; }
        public string? ResolutionNote { get; init; }
        public DateTime? ResolvedAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
