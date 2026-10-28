using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Orders.Commands.EstimateFare;

namespace ModMove.WebApi.Controllers.Public;

public sealed class FareEstimateController : ApiControllerBase
{
    /// <summary>Price of a trip before booking (B1): Google Maps km, vehicle fit (GB-10), loading fee, coupon (GB-21).</summary>
    [HttpPost]
    public Task<FareEstimateResponse> Estimate(EstimateFareCommand command, CancellationToken cancellationToken) =>
        Sender.Send(command, cancellationToken);
}
