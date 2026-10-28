using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.DriverOnboarding.Commands.SubmitDriverApplication;
using ModMove.Application.Features.DriverOnboarding.Queries.GetMyApplicationStatus;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary>Driver only (RoleConvention): api/driver/onboarding. Driver Verification Upload screen (UC-DRV-03..05).</summary>
public sealed class OnboardingController : ApiControllerBase
{
    /// <summary>UC-DRV-04: latest application version, its status and, when Rejected, the reasons.</summary>
    [HttpGet("status")]
    public Task<DriverOnboardingStatusResponse> GetStatus(CancellationToken cancellationToken) =>
        Sender.Send(new GetMyApplicationStatusQuery(), cancellationToken);

    /// <summary>
    /// UC-DRV-03 / UC-DRV-05: submit a new version â†’ 201 { id }. Pending or Approved â†’ 422 APPLICATION_NOT_EDITABLE;
    /// citizen ID, licence or plate used by another driver â†’ 409.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Submit(SubmitDriverApplicationCommand command, CancellationToken cancellationToken)
    {
        var id = await Sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetStatus), new { id }, new { id });
    }
}

