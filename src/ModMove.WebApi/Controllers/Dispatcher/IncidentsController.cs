using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Incidents.Commands.CancelIncident;
using ModMove.Application.Features.Incidents.Commands.CancelOrderByIncident;
using ModMove.Application.Features.Incidents.Commands.ReassignIncidentDriver;
using ModMove.Application.Features.Incidents.Commands.ResolveIncidentContinue;
using ModMove.Application.Features.Incidents.Commands.StartIncidentHandling;
using ModMove.Application.Features.Incidents.Queries.GetIncidentById;
using ModMove.Application.Features.Incidents.Queries.GetReplacementDrivers;
using ModMove.Application.Features.Incidents.Queries.ListIncidents;
using ModMove.Application.Features.Refunds.Commands.ProposeIncidentRefundResolution;
using ModMove.Application.Features.Refunds.Commands.RequestIncidentRefundInfo;

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
    /// proof of delivery, status histories, and refund claim (if any).
    /// </summary>
    [HttpGet("{id:long}")]
    public Task<IncidentDetailResponse> GetById(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetIncidentByIdQuery(id), cancellationToken);

    /// <summary>UC-DIS-05: claim an Open incident for handling (transitions to InProgress).</summary>
    [HttpPost("{id:long}/start")]
    public async Task<IActionResult> Start(long id, CancellationToken cancellationToken)
    {
        await Sender.Send(new StartIncidentHandlingCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>UC-DIS-05: resolve an incident by instructing the assigned driver to continue the trip.</summary>
    [HttpPost("{id:long}/resolve")]
    public async Task<IActionResult> ResolveContinue(long id, IncidentReasonBody body, CancellationToken cancellationToken)
    {
        await Sender.Send(new ResolveIncidentContinueCommand(id, body.Reason), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// UC-DIS-06 (DECISIONS B5, 6.4, 7.2, GB-16): eligible online drivers near the incident who can take over the order,
    /// sorted by distance ascending.
    /// </summary>
    [HttpGet("{id:long}/replacement-drivers")]
    public Task<IReadOnlyList<ReplacementDriverResponse>> GetReplacementDrivers(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetReplacementDriversQuery(id), cancellationToken);

    /// <summary>UC-DIS-06 (DECISIONS 7.2): reassign the order to an eligible replacement driver and resolve the incident.</summary>
    [HttpPost("{id:long}/reassign")]
    public async Task<IActionResult> ReassignDriver(long id, ReassignIncidentDriverBody body, CancellationToken cancellationToken)
    {
        await Sender.Send(new ReassignIncidentDriverCommand(id, body.NewDriverProfileId, body.Reason), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// UC-DIS-07 (DECISIONS 2.4, 2.5, 2.6, 4.3): cancel the order due to an incident. Requires <c>cargoDisposition</c>
    /// when cargo is on the vehicle (<c>PickedUp</c> / <c>InTransit</c>); <c>refund: true</c> is allowed only for paid PayOS orders.
    /// </summary>
    [HttpPost("{id:long}/cancel-order")]
    public async Task<IActionResult> CancelOrder(long id, CancelOrderByIncidentBody body, CancellationToken cancellationToken)
    {
        await Sender.Send(new CancelOrderByIncidentCommand(id, body.Reason, body.Refund, body.CargoDisposition), cancellationToken);
        return NoContent();
    }

    /// <summary>UC-DIS-10: cancel/dismiss an incident report that has no valid basis.</summary>
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, IncidentReasonBody body, CancellationToken cancellationToken)
    {
        await Sender.Send(new CancelIncidentCommand(id, body.Reason), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// UC-DIS-14 (DECISIONS 7.4): ask the customer for more evidence on the refund requested because of this incident.
    /// </summary>
    [HttpPost("{id:long}/refund/request-info")]
    public async Task<IActionResult> RequestRefundInfo(long id, IncidentRefundInfoBody body, CancellationToken cancellationToken)
    {
        await Sender.Send(new RequestIncidentRefundInfoCommand(id, body.Note), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// UC-DIS-09 / UC-DIS-17: propose a full, partial or no refund (amount 0) for the refund requested because of this
    /// incident, sending it to the Administrator for approval.
    /// </summary>
    [HttpPost("{id:long}/refund/proposal")]
    public async Task<IActionResult> ProposeRefund(long id, IncidentRefundProposalBody body, CancellationToken cancellationToken)
    {
        await Sender.Send(new ProposeIncidentRefundResolutionCommand(id, body.Amount, body.Reason), cancellationToken);
        return NoContent();
    }
}

public sealed record IncidentReasonBody(string Reason);
public sealed record ReassignIncidentDriverBody(long NewDriverProfileId, string Reason);
public sealed record CancelOrderByIncidentBody(string Reason, bool Refund, CargoDisposition? CargoDisposition = null);
public sealed record IncidentRefundInfoBody(string Note);
public sealed record IncidentRefundProposalBody(decimal Amount, string Reason);
