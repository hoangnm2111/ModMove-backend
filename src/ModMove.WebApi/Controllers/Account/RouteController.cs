using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModMove.Application.Features.Drivers.Queries.GetDriverRoute;
using ModMove.WebApi.Controllers.Public;

namespace ModMove.WebApi.Controllers.Account;

public sealed class RouteController : ApiControllerBase
{
    /// <summary>
    /// Driving route between two points for the web map of any signed-in role (customer tracking, Dispatcher, Driver):
    /// <c>distanceKm</c>, <c>durationMinutes</c>, encoded <c>polyline</c> (5 decimals). Same answer and cache as
    /// <c>GET api/driver/route</c>; each cache miss spends a MapVina call, so it is limited per user (429 TOO_MANY_REQUESTS).
    /// </summary>
    [HttpGet]
    [EnableRateLimiting(AuthRateLimit.UserPolicy)]
    public Task<DriverRouteResponse> Get([FromQuery] GetDriverRouteQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);
}
