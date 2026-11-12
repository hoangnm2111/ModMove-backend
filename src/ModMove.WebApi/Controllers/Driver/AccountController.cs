using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Users.Commands.DeleteMyDriverAccount;

namespace ModMove.WebApi.Controllers.Driver;

public sealed class AccountController : ApiControllerBase
{
    /// <summary>
    /// Q12: delete (deactivate) your driver account. A running order → 422 ACCOUNT_HAS_OPEN_ORDERS; a waiting withdrawal →
    /// ACCOUNT_HAS_PENDING_WITHDRAWAL; money left in or owed to the wallet → ACCOUNT_HAS_WALLET_BALANCE.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteMyDriverAccountCommand(), cancellationToken);
        return NoContent();
    }
}
