using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Incidents.Queries.GetDriverIncidents;

internal sealed class GetDriverIncidentsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IFileStorage storage) : IQueryHandler<GetDriverIncidentsQuery, PagedResult<DriverIncidentResponse>>
{
    // Includes both incidents reported by this driver (even after ReassignDriver moved the order to another driver)
    // and incidents on orders currently assigned to this driver (IDOR-safe, C10).
    private const string FromSql = """
        FROM in_transit_incidents i
        JOIN shipping_orders o ON o.id = i.order_id
        WHERE (i.reported_by = @UserId OR (@DriverProfileId IS NOT NULL AND o.driver_profile_id = @DriverProfileId))
          AND (@OrderId IS NULL OR i.order_id = @OrderId)
          AND (@Status IS NULL OR i.resolution_status = @Status)
        """;

    private const string CountSql = $"SELECT COUNT(1) {FromSql}";

    private const string ItemsSql = $"""
        SELECT i.id AS Id,
               i.order_id AS OrderId,
               o.order_code AS OrderCode,
               o.order_status AS OrderStatus,
               i.incident_type AS IncidentType,
               i.description AS Description,
               i.photo_urls AS PhotoUrls,
               i.latitude AS Latitude,
               i.longitude AS Longitude,
               i.reported_by AS ReportedBy,
               i.resolution_status AS ResolutionStatus,
               i.resolution_decision AS ResolutionDecision,
               i.resolution_note AS ResolutionNote,
               i.created_at AS CreatedAt,
               i.resolved_at AS ResolvedAt
        {FromSql}
        ORDER BY i.created_at DESC, i.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<DriverIncidentResponse>> Handle(
        GetDriverIncidentsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var context = await DriverAccess.LoadReadContextAsync(connection, userId, cancellationToken);

        var parameters = new
        {
            UserId = userId,
            context.DriverProfileId,
            request.OrderId,
            Status = request.Status?.ToString(),
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));
        if (total == 0)
            return new PagedResult<DriverIncidentResponse>([], request.Page, request.PageSize, 0);

        var rows = await connection.QueryAsync<DriverIncidentRow>(
            new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => r.ToResponse(userId, storage)).ToList();
        return new PagedResult<DriverIncidentResponse>(items, request.Page, request.PageSize, total);
    }
}

internal sealed class GetDriverIncidentDetailQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IFileStorage storage) : IQueryHandler<GetDriverIncidentDetailQuery, DriverIncidentResponse>
{
    private const string Sql = """
        SELECT i.id AS Id,
               i.order_id AS OrderId,
               o.order_code AS OrderCode,
               o.order_status AS OrderStatus,
               i.incident_type AS IncidentType,
               i.description AS Description,
               i.photo_urls AS PhotoUrls,
               i.latitude AS Latitude,
               i.longitude AS Longitude,
               i.reported_by AS ReportedBy,
               i.resolution_status AS ResolutionStatus,
               i.resolution_decision AS ResolutionDecision,
               i.resolution_note AS ResolutionNote,
               i.created_at AS CreatedAt,
               i.resolved_at AS ResolvedAt
        FROM in_transit_incidents i
        JOIN shipping_orders o ON o.id = i.order_id
        WHERE i.id = @IncidentId
          AND (i.reported_by = @UserId OR (@DriverProfileId IS NOT NULL AND o.driver_profile_id = @DriverProfileId))
        """;

    public async Task<DriverIncidentResponse> Handle(
        GetDriverIncidentDetailQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var context = await DriverAccess.LoadReadContextAsync(connection, userId, cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<DriverIncidentRow>(
            new CommandDefinition(
                Sql,
                new { request.IncidentId, UserId = userId, context.DriverProfileId },
                cancellationToken: cancellationToken))
            ?? throw new NotFoundException("Incident", request.IncidentId);

        return row.ToResponse(userId, storage);
    }
}

internal sealed class DriverIncidentRow
{
    public long Id { get; init; }
    public long OrderId { get; init; }
    public string OrderCode { get; init; } = "";
    public string OrderStatus { get; init; } = "";
    public string IncidentType { get; init; } = "";
    public string Description { get; init; } = "";
    public string? PhotoUrls { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public long ReportedBy { get; init; }
    public string ResolutionStatus { get; init; } = "";
    public string? ResolutionDecision { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }

    public DriverIncidentResponse ToResponse(long driverUserId, IFileStorage storage)
    {
        var reportedByYou = ReportedBy == driverUserId;
        return new DriverIncidentResponse(
            Id,
            OrderId,
            OrderCode,
            OrderStatus,
            IncidentType,
            // DECISIONS 8.3: someone else's report (the customer's, a Dispatcher's note, a previous driver's) goes to Dispatchers only.
            reportedByYou ? Description : "Có sự cố trên đơn này, Điều phối viên đang xử lý.",
            reportedByYou ? SqlHelpers.ParseJsonList(PhotoUrls).Select(storage.CreateViewUrl).ToList() : [],
            reportedByYou ? Latitude : null,
            reportedByYou ? Longitude : null,
            reportedByYou,
            ResolutionStatus,
            ResolutionDecision,
            ResolutionNote,
            SqlHelpers.ToUtc(CreatedAt),
            SqlHelpers.ToUtc(ResolvedAt));
    }
}

