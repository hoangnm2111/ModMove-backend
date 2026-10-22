using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModMove.Application.Features.Users.Commands.SubmitSupportRequest;

namespace ModMove.WebApi.Controllers.Public;

// Anonymous write: rate limited so a bot cannot flood the table.
[EnableRateLimiting(AuthRateLimit.Policy)]
public sealed class SupportRequestsController : ApiControllerBase
{
    /// <summary>A guest's question before registering (UC-GST-05).</summary>
    [HttpPost]
    public async Task<IActionResult> Submit(SubmitSupportRequestCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }
}
