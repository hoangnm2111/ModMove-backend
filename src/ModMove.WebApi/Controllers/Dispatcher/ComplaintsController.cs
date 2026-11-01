using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Refunds.Queries.ListComplaints;

namespace ModMove.WebApi.Controllers.Dispatcher;

public sealed class ComplaintsController : ApiControllerBase
{
    /// <summary>
    /// UC-DIS-11: list complaints filed by customers within 24 hours of delivery.
    /// Optional filters <c>stage</c> (NeedsReview, WaitingForCustomer, CustomerReplied, ProposedToAdmin, Closed),
    /// <c>search</c> (order code); paging <c>?page=1&amp;pageSize=20</c>.
    /// </summary>
    [HttpGet]
    public Task<PagedResult<ComplaintListItemResponse>> List([FromQuery] ListComplaintsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>
    /// UC-DIS-12 &amp; UC-DIS-13: view comprehensive complaint details and cross-check evidence against pickup photos,
    /// proof-of-delivery photo, customer ratings, trip status history, and reported in-transit incidents.
    /// </summary>
    [HttpGet("{id:long}")]
    public Task<ModMove.Application.Features.Refunds.Queries.GetComplaintById.ComplaintDetailResponse> GetById(
        long id, CancellationToken cancellationToken) =>
        Sender.Send(new ModMove.Application.Features.Refunds.Queries.GetComplaintById.GetComplaintByIdQuery(id), cancellationToken);

    /// <summary>
    /// UC-DIS-14 (DECISIONS 7.4): request additional information or evidence from the customer regarding the complaint.
    /// </summary>
    [HttpPost("{id:long}/request-info")]
    public async Task<IActionResult> RequestInfo(long id, RequestComplaintInfoBody body, CancellationToken cancellationToken)
    {
        await Sender.Send(new ModMove.Application.Features.Refunds.Commands.RequestComplaintInfo.RequestComplaintInfoCommand(id, body.Note), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// UC-DIS-15 / UC-DIS-09 / UC-DIS-17: propose full, partial or no refund (amount 0) with a justification,
    /// submitting the proposal to the Administrator for approval.
    /// </summary>
    [HttpPost("{id:long}/proposal")]
    public async Task<IActionResult> Propose(long id, ProposeComplaintResolutionBody body, CancellationToken cancellationToken)
    {
        await Sender.Send(new ModMove.Application.Features.Refunds.Commands.ProposeComplaintResolution.ProposeComplaintResolutionCommand(id, body.Amount, body.Reason), cancellationToken);
        return NoContent();
    }
}

public sealed record RequestComplaintInfoBody(string Note);
public sealed record ProposeComplaintResolutionBody(decimal Amount, string Reason);
