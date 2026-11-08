using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModMove.Application.Features.Orders.Commands.EstimateFare;

namespace ModMove.WebApi.Controllers.Public;

// Anonymous and each call uses 3 MapVina lookups (2 geocode + 1 route) of the team's shared daily quota (C3): same per-IP limit as auth.
[EnableRateLimiting(AuthRateLimit.Policy)]
public sealed class FareEstimateController : ApiControllerBase
{
    /// <summary>Price of a trip before booking (B1): MapVina km, vehicle fit (GB-10), loading fee, coupon (GB-21).</summary>
    [HttpPost]
    public Task<FareEstimateResponse> Estimate(EstimateFareCommand command, CancellationToken cancellationToken) =>
        Sender.Send(command, cancellationToken);
}
