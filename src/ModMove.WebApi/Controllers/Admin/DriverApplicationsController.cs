using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.DriverOnboarding.Commands.ApproveDriverApplication;
using ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;
using ModMove.Application.Features.DriverOnboarding.Queries.GetApplicationDetails;
using ModMove.Application.Features.DriverOnboarding.Queries.GetPendingApplications;

namespace ModMove.WebApi.Controllers.Admin;

/// <summary>Admin only: api/admin/driver-applications. Driver onboarding review and document evaluation, screens #33 &amp; #34.</summary>
[Route("api/admin/driver-applications")]
public sealed class DriverApplicationsController : ApiControllerBase
{
    /// <summary>UC-ADM-08: FIFO queue of pending driver applications waiting for review.</summary>
    [HttpGet("pending")]
    public Task<PagedResult<PendingApplicationDto>> GetPending(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Sender.Send(new GetPendingApplicationsQuery(page, pageSize), cancellationToken);

    /// <summary>UC-ADM-09: view full details of a driver application including vehicle, documents, and submission history.</summary>
    [HttpGet("{id:long}")]
    public Task<DriverApplicationDetailsResponse> GetDetails(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetApplicationDetailsQuery(id), cancellationToken);

    /// <summary>UC-ADM-09: approve driver application, updates/creates vehicle and provisions driver wallet (Rule GB-16).</summary>
    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken cancellationToken)
    {
        await Sender.Send(new ApproveDriverApplicationCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>UC-ADM-09: reject driver application with reason per item.</summary>
    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, RejectDriverApplicationRequest body, CancellationToken cancellationToken)
    {
        await Sender.Send(new RejectDriverApplicationCommand(id, body.Reasons), cancellationToken);
        return NoContent();
    }
}

public sealed record RejectDriverApplicationRequest(IReadOnlyList<string> Reasons);
