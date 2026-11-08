using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Ratings.Queries.GetDriverRatings;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary>Driver only (RoleConvention): api/driver/ratings. Driver rating summary and customer reviews (DECISIONS 7.5, 8.3).</summary>
public sealed class RatingsController : ApiControllerBase
{
    /// <summary>Average rating, 1..5 star counts and paginated customer reviews received by the signed-in driver.</summary>
    [HttpGet]
    public Task<DriverRatingsResponse> GetMine(
        [FromQuery] GetDriverRatingsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);
}

