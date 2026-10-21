using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Auth.Commands.Logout;

namespace ModMove.WebApi.Controllers.Account;

public sealed class LogoutController : ApiControllerBase
{
    /// <summary>Stop push notifications to this device; the client then deletes its token.</summary>
    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await Sender.Send(new LogoutCommand(), cancellationToken);
        return NoContent();
    }
}
