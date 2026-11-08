using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByDriver;
using ModMove.Application.Features.Incidents.Queries.GetDriverIncidents;
using ModMove.WebApi.Controllers.Public;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary>Driver only (RoleConvention): api/driver/incidents. Report an in-transit incident and track Dispatcher resolution (UC-DRV-18, 19; DECISIONS 6.1, 7.1).</summary>
public sealed class IncidentsController : ApiControllerBase
{
    /// <summary>Incidents reported by you or on orders currently assigned to you, newest first (UC-DRV-19).</summary>
    [HttpGet]
    public Task<PagedResult<DriverIncidentResponse>> GetMine(
        [FromQuery] GetDriverIncidentsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>Detail and Dispatcher resolution of a single incident (UC-DRV-19). Not yours -> 404.</summary>
    [HttpGet("{id:long}")]
    public Task<DriverIncidentResponse> GetById(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetDriverIncidentDetailQuery(id), cancellationToken);

    /// <summary>
    /// Report a problem on your active order (Accepted..InTransit, DECISIONS 6.1, 7.1). Alerts Dispatchers and informs the Customer.
    /// Rate-limited per user.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(AuthRateLimit.UserPolicy)]
    public async Task<IActionResult> Report(
        ReportIncidentByDriverCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}

