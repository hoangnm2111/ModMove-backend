using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Payments.Commands.TopUpDeposit;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary>Driver only (RoleConvention): api/driver/wallet. Deposit top-up via PayOS (UC-DRV-21, DECISIONS 5.2).</summary>
public sealed class WalletController : ApiControllerBase
{
    /// <summary>
    /// UC-DRV-21, DECISIONS 5.2, GB-13: creates a 10-minute PayOS payment link and QR code for a driver deposit top-up
    /// (minimum 50,000 VND). Calling again replaces any still-Pending session.
    /// </summary>
    [HttpPost("topup")]
    [HttpPost("top-up")]
    public Task<TopUpDepositResponse> TopUpDeposit(
        TopUpDepositRequest request, CancellationToken cancellationToken) =>
        Sender.Send(new TopUpDepositCommand(request.Amount, request.Platform), cancellationToken);
}

