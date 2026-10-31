using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;
using ModMove.Application.Features.Incidents.Queries.GetMyIncidents;

namespace ModMove.WebApi.Controllers.Customer;

/// <summary>Customer side of incidents (UC-CUS-15, 16; DECISIONS 2.2, GB-17).</summary>
public sealed class IncidentsController : ApiControllerBase
{
    /// <summary>Incidents on your orders and the Dispatcher's decision, newest first.</summary>
    [HttpGet]
    public Task<PagedResult<MyIncidentResponse>> GetMine([FromQuery] GetMyIncidentsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>Report a problem while the driver has your order (Arrived, PickedUp, InTransit).</summary>
    [HttpPost]
    public async Task<IActionResult> Report(ReportIncidentByCustomerCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
