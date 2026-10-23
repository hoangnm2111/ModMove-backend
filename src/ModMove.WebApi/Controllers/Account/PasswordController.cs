using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModMove.Application.Features.Auth.Commands.ChangePassword;
using ModMove.WebApi.Controllers.Public;

namespace ModMove.WebApi.Controllers.Account;

// Rate limited like sign-in: a stolen token must not allow unlimited guesses of the current password.
[EnableRateLimiting(AuthRateLimit.Policy)]
public sealed class PasswordController : ApiControllerBase
{
    /// <summary>Change your own password (any role). Wrong current password → 422 WRONG_PASSWORD.</summary>
    [HttpPost]
    public async Task<IActionResult> Change(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }
}
