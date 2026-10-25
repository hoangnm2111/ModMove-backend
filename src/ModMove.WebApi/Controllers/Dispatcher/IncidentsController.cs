using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Incidents.Queries.ListIncidents;

namespace ModMove.WebApi.Controllers.Dispatcher;

public sealed class IncidentsController : ApiControllerBase
{
    /// <summary>
    /// UC-DIS-03: queue of incident reports. Optional filters <c>status</c>, <c>incidentType</c>, <c>search</c> (order code);
    /// paging <c>?page=1&amp;pageSize=20</c>. Open first, then InProgress, then closed; the oldest first inside each group.
    /// </summary>
    [HttpGet]
    public Task<PagedResult<IncidentListItemResponse>> List([FromQuery] ListIncidentsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>
    /// UC-DIS-04: view the full details of an in-transit incident including order, cargo, customer, driver,
    /// status histories, and refund claim (if any).
    /// </summary>
    [HttpGet("{id:long}")]
    public Task<ModMove.Application.Features.Incidents.Queries.GetIncidentById.IncidentDetailResponse> GetById(
        long id, CancellationToken cancellationToken) =>
        Sender.Send(new ModMove.Application.Features.Incidents.Queries.GetIncidentById.GetIncidentByIdQuery(id), cancellationToken);
}
