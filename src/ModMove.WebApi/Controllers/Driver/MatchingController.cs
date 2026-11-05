using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Matching.Commands.AcceptOrder;
using ModMove.Application.Features.Matching.Queries.GetNearbyOrders;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary>Driver only (RoleConvention): api/driver/matching. Dispatch Job Alert screen (UC-DRV-08..10).</summary>
public sealed class MatchingController : ApiControllerBase
{
    /// <summary>
    /// UC-DRV-08: Pending orders within <c>MATCHING_RADIUS_KM</c> (5 km) of the driver's last GPS point that match the
    /// driver's vehicle type and (for cash orders) deposit balance, sorted nearest pickup first.
    /// </summary>
    [HttpGet("orders")]
    public Task<IReadOnlyList<NearbyOrderDto>> GetNearbyOrders(CancellationToken cancellationToken) =>
        Sender.Send(new GetNearbyOrdersQuery(), cancellationToken);

    /// <summary>
    /// UC-DRV-10, GB-08: the first eligible driver to accept takes the order; a second driver gets 409 CONFLICT.
    /// </summary>
    [HttpPost("orders/{id:long}/accept")]
    public async Task<IActionResult> AcceptOrder(long id, CancellationToken cancellationToken)
    {
        await Sender.Send(new AcceptOrderCommand(id), cancellationToken);
        return NoContent();
    }
}

