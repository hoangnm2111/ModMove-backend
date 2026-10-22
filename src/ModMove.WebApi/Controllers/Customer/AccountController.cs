using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Users.Commands.DeleteMyAccount;

namespace ModMove.WebApi.Controllers.Customer;

public sealed class AccountController : ApiControllerBase
{
    /// <summary>Delete (deactivate) your customer account. Open orders → 422 ACCOUNT_HAS_OPEN_ORDERS.</summary>
    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteMyAccountCommand(), cancellationToken);
        return NoContent();
    }
}
