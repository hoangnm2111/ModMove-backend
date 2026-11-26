using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Users.Commands.UpdateMyProfile;
using ModMove.Application.Features.Users.Queries.GetMyProfile;

namespace ModMove.WebApi.Controllers.Account;

public sealed class ProfileController : ApiControllerBase
{
    [HttpGet]
    public Task<ProfileResponse> Get(CancellationToken cancellationToken) =>
        Sender.Send(new GetMyProfileQuery(), cancellationToken);

    /// <summary>Phone already used by another account → 409 PHONE_TAKEN (GB-01).</summary>
    [HttpPut]
    public async Task<IActionResult> Update(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }
}
