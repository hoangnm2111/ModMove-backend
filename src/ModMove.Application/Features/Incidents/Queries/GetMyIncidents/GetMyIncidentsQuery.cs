using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Incidents.Queries.GetMyIncidents;

/// <summary>
/// UC-CUS-16: incidents on the signed-in customer's orders, whoever reported them (customer or driver), with the
/// Dispatcher's decision once handled. Newest first.
/// </summary>
public sealed record GetMyIncidentsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<MyIncidentResponse>>;

public sealed record MyIncidentResponse(
    long Id, long OrderId, string OrderCode, string IncidentType, string Description, IReadOnlyList<string> PhotoUrls,
    bool ReportedByYou, string ResolutionStatus, string? ResolutionDecision, string? ResolutionNote,
    DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt);
