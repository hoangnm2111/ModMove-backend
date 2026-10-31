using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Auth.Commands.Logout;

namespace ModMove.WebApi.Controllers.Account;

public sealed class LogoutController : ApiControllerBase
{
    /// <summary>
    /// Sign out: every refresh token is revoked (9.1). The app sends <c>{ fcmToken }</c> so its pushes stop if that token
    /// is still on file (9.2); the web sends no body. The client deletes its tokens.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] LogoutCommand? command,
        CancellationToken cancellationToken)
    {
        await Sender.Send(command ?? new LogoutCommand(), cancellationToken);
        return NoContent();
    }
}
