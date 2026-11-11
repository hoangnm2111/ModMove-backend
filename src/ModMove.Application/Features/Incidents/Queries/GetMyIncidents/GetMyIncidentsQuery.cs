using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Incidents.Queries.GetMyIncidents;

/// <summary>
/// UC-CUS-16: incidents on the signed-in customer's orders, whoever reported them (customer or driver), with the
/// Dispatcher's decision once handled. Newest first. Optional <c>orderId</c> and <c>status</c> filters. An incident the
/// driver reported comes with the driver's own description and photos (DECISIONS 8.3).
/// </summary>
public sealed record GetMyIncidentsQuery(
    int Page = 1, int PageSize = 20, long? OrderId = null, IncidentResolutionStatus? Status = null)
    : IQuery<PagedResult<MyIncidentResponse>>;

public sealed record MyIncidentResponse(
    long Id, long OrderId, string OrderCode, string IncidentType, string Description, IReadOnlyList<string> PhotoUrls,
    bool ReportedByYou, string ResolutionStatus, string? ResolutionDecision, string? ResolutionNote,
    DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt);
