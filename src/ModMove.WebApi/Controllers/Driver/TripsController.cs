using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Trips.Commands.CollectCash;
using ModMove.Application.Features.Trips.Commands.ConfirmArrival;
using ModMove.Application.Features.Trips.Commands.ConfirmPickup;
using ModMove.Application.Features.Trips.Commands.DeliverTrip;
using ModMove.Application.Features.Trips.Commands.NotifyArrivalAtDropoff;
using ModMove.Application.Features.Trips.Queries.GetDriverTripDetail;
using ModMove.Application.Features.Trips.Queries.GetDriverTrips;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary>
/// Driver only (RoleConvention): api/driver/trips. Trip execution and history (UC-DRV-11..17; DECISIONS 2.7, 6.1, 6.2, 6.3, 7.2).
/// Drivers cannot self-cancel an accepted order (DECISIONS 6.1); they report an incident at api/driver/incidents instead.
/// </summary>
public sealed class TripsController : ApiControllerBase
{
    /// <summary>Your assigned orders, newest first (UC-DRV-11). <c>view</c>: All, Active, History, Completed, Cancelled.</summary>
    [HttpGet]
    public Task<PagedResult<DriverTripSummaryResponse>> GetMine(
        [FromQuery] GetDriverTripsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>Your currently running trip (Accepted..InTransit), or 204 NoContent when idle (UC-DRV-11).</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var active = await Sender.Send(new GetActiveDriverTripQuery(), cancellationToken);
        return active is null ? NoContent() : Ok(active);
    }

    /// <summary>Full detail of an order assigned to you, including cargo, proof, handover point, history and incidents (UC-DRV-11..15, 19).</summary>
    [HttpGet("{id:long}")]
    public Task<DriverTripDetailResponse> GetById(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetDriverTripDetailQuery(id), cancellationToken);

    /// <summary>Confirm arrival at the pickup or handover point: Accepted -> Arrived (UC-DRV-12).</summary>
    [HttpPost("{id:long}/arrive")]
    public async Task<IActionResult> ConfirmArrival(long id, CancellationToken cancellationToken)
    {
        await Sender.Send(new ConfirmArrivalCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Upload 2..5 pickup photos (Arrived -> PickedUp) or resubmit rejected pickup photos up to 5 times (UC-DRV-13, GB-11, DECISIONS 6.3).</summary>
    [HttpPost("{id:long}/pickup")]
    public async Task<IActionResult> ConfirmPickup(
        long id, ConfirmPickupRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new ConfirmPickupCommand(id, request.PhotoUrls), cancellationToken);
        return NoContent();
    }

    /// <summary>Notify the customer that the driver has arrived at the drop-off location while InTransit; does not change order status (UC-DRV-15).</summary>
    [HttpPost("{id:long}/arrive-dropoff")]
    public async Task<IActionResult> NotifyArrivalAtDropoff(long id, CancellationToken cancellationToken)
    {
        await Sender.Send(new NotifyArrivalAtDropoffCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Confirm cash collection on a Cash order (UC-DRV-17): at pickup (Arrived/PickedUp) when Sender pays, or at drop-off (InTransit after arrive-dropoff) when Receiver pays. Coordinates are optional; the server does not check the distance (GB-52).</summary>
    [HttpPost("{id:long}/collect-cash")]
    public async Task<IActionResult> CollectCash(
        long id, CollectCashRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new CollectCashCommand(id, request.Latitude, request.Longitude), cancellationToken);
        return NoContent();
    }

    /// <summary>Complete delivery with a proof-of-delivery photo: InTransit -> Delivered (UC-DRV-16, GB-12, DECISIONS 2.7, 6.2).</summary>
    [HttpPost("{id:long}/deliver")]
    public async Task<IActionResult> Deliver(
        long id, DeliverTripRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeliverTripCommand(id, request.DeliveryPhotoUrl, request.CashCollected, request.Latitude, request.Longitude), cancellationToken);
        return NoContent();
    }
}

