using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Auth.Commands.Logout;

namespace ModMove.WebApi.Controllers.Account;

public sealed class LogoutController : ApiControllerBase
{
    /// <summary>Sign out on every device: refresh tokens are revoked and pushes stop; the client deletes its tokens.</summary>
    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await Sender.Send(new LogoutCommand(), cancellationToken);
        return NoContent();
    }
}
