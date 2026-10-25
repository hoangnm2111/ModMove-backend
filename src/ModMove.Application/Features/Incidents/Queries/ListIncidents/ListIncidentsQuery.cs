using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Incidents.Queries.ListIncidents;

/// <summary>
/// UC-DIS-03: the Dispatcher's queue of incident reports. Every Dispatcher sees every incident. Without filters the
/// queue shows all statuses: Open first, then InProgress, then closed, the longest-waiting first inside each group.
/// </summary>
/// <param name="Status">Only incidents in this resolution status.</param>
/// <param name="IncidentType">Only incidents of this type.</param>
/// <param name="Search">Part of the order code; <c>%</c> and <c>_</c> are literal.</param>
public sealed record ListIncidentsQuery(
    IncidentResolutionStatus? Status,
    IncidentType? IncidentType,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<IncidentListItemResponse>>;

public sealed record IncidentListItemResponse(
    long Id,
    long OrderId,
    string OrderCode,
    string OrderStatus,
    string IncidentType,
    string Description,
    long ReportedById,
    string ReporterName,
    string ReporterRole,
    string ResolutionStatus,
    long? HandledById,
    string? HandledByName,
    DateTimeOffset CreatedAt);
