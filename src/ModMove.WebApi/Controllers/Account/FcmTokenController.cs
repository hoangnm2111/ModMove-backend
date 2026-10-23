using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Users.Commands.UpdateFcmToken;

namespace ModMove.WebApi.Controllers.Account;

public sealed class FcmTokenController : ApiControllerBase
{
    /// <summary>Mobile app: register this device for push notifications after sign-in.</summary>
    [HttpPut]
    public async Task<IActionResult> Update(UpdateFcmTokenCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }
}
