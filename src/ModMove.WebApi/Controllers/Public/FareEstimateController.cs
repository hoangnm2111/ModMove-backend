using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModMove.Application.Features.Orders.Commands.EstimateFare;

namespace ModMove.WebApi.Controllers.Public;

// Anonymous and each call costs 3 paid Google requests on the team's billing account (C3): same per-IP limit as auth.
[EnableRateLimiting(AuthRateLimit.Policy)]
public sealed class FareEstimateController : ApiControllerBase
{
    /// <summary>Price of a trip before booking (B1): Google Maps km, vehicle fit (GB-10), loading fee, coupon (GB-21).</summary>
    [HttpPost]
    public Task<FareEstimateResponse> Estimate(EstimateFareCommand command, CancellationToken cancellationToken) =>
        Sender.Send(command, cancellationToken);
}
