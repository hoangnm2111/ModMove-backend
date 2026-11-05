using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Drivers.Commands.SetOnlineStatus;
using ModMove.Application.Features.Drivers.Queries.GetDriverStatus;
using ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;

namespace ModMove.WebApi.Controllers.Driver;

public sealed record UpdateDriverLocationRequest(decimal Latitude, decimal Longitude);

/// <summary>Driver only (RoleConvention): api/driver. Driver home screen: go Online or Offline and see the current state (UC-DRV-08).</summary>
[Route("api/driver")]
public sealed class DriverController(ICurrentUserService currentUser) : ApiControllerBase
{
    /// <summary>Approval, Online state, wallet balances, current order and whether the last GPS point is stale (6.4).</summary>
    [HttpGet("status")]
    public Task<DriverStatusResponse> GetStatus(CancellationToken cancellationToken) =>
        Sender.Send(new GetDriverStatusQuery(), cancellationToken);

    /// <summary>
    /// GB-03: <c>{ isOnline }</c> → 204. Going Online needs an Approved profile (else 422 BUSINESS_RULE);
    /// an order in progress is not affected by going Offline.
    /// </summary>
    [HttpPatch("online-status")]
    public async Task<IActionResult> SetOnlineStatus(SetOnlineStatusCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Updates the driver's current GPS position. Also streamed via SignalR /hubs/tracking (SendLocation).
    /// </summary>
    [HttpPost("location")]
    public async Task<IActionResult> UpdateLocation(UpdateDriverLocationRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        await Sender.Send(new UpdateDriverLocationCommand(userId, request.Latitude, request.Longitude), cancellationToken);
        return NoContent();
    }
}

