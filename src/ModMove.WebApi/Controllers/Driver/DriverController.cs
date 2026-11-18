using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Drivers.Commands.SetOnlineStatus;
using ModMove.Application.Features.Drivers.Queries.GetDriverProfile;
using ModMove.Application.Features.Drivers.Queries.GetDriverStatus;
using ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary><c>accuracy</c> in meters (B19), <c>isMocked</c> (B20), <c>recordedAt</c> for a point sent late (B21): all optional.</summary>
public sealed record UpdateDriverLocationRequest(
    decimal Latitude,
    decimal Longitude,
    decimal? Accuracy = null,
    bool IsMocked = false,
    DateTimeOffset? RecordedAt = null);

/// <summary>Driver only (RoleConvention): api/driver. Driver profile, home screen, Online/Offline state and GPS (UC-DRV-06, UC-DRV-08).</summary>
[Route("api/driver")]
public sealed class DriverController(ICurrentUserService currentUser) : ApiControllerBase
{
    /// <summary>UC-DRV-06: personal credentials, bank account, registered vehicle (dimensions, certificates) and rating summary.</summary>
    [HttpGet("profile")]
    public Task<DriverProfileDetailResponse> GetProfile(CancellationToken cancellationToken) =>
        Sender.Send(new GetDriverProfileQuery(), cancellationToken);

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
    /// Updates the driver's current GPS position; the driver app's channel (B12). SignalR SendLocation does the same.
    /// 204 also when an inaccurate point is dropped (B19); 422 MOCK_LOCATION (B20); 400 when recordedAt is out of range (B21).
    /// </summary>
    [HttpPost("location")]
    public async Task<IActionResult> UpdateLocation(UpdateDriverLocationRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        await Sender.Send(
            new UpdateDriverLocationCommand(userId, request.Latitude, request.Longitude, request.Accuracy, request.IsMocked, request.RecordedAt),
            cancellationToken);
        return NoContent();
    }
}

