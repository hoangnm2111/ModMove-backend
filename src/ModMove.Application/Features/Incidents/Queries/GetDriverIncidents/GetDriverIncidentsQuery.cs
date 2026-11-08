using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Incidents.Queries.GetDriverIncidents;

/// <summary>
/// UC-DRV-19: incidents reported by the driver or attached to orders currently assigned to the driver, newest first.
/// </summary>
public sealed record GetDriverIncidentsQuery(
    int Page = 1,
    int PageSize = 20,
    long? OrderId = null,
    IncidentResolutionStatus? Status = null) : IQuery<PagedResult<DriverIncidentResponse>>;

/// <summary>UC-DRV-19: single incident detail and Dispatcher decision for the signed-in driver.</summary>
public sealed record GetDriverIncidentDetailQuery(long IncidentId) : IQuery<DriverIncidentResponse>;

public sealed record DriverIncidentResponse(
    long Id,
    long OrderId,
    string OrderCode,
    string OrderStatus,
    string IncidentType,
    string Description,
    IReadOnlyList<string> PhotoUrls,
    decimal? Latitude,
    decimal? Longitude,
    bool ReportedByYou,
    string ResolutionStatus,
    string? ResolutionDecision,
    string? ResolutionNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

