using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Ratings.Commands.RateTrip;
using ModMove.Application.Features.Ratings.Queries.GetMyRatings;

namespace ModMove.WebApi.Controllers.Customer;

public sealed class RatingsController : ApiControllerBase
{
    /// <summary>Ratings you have given, newest first (UC-CUS-26).</summary>
    [HttpGet]
    public Task<PagedResult<MyRatingResponse>> GetMine([FromQuery] GetMyRatingsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>Rate the driver of a completed trip, once, within 1 day of completion (UC-CUS-25).</summary>
    [HttpPost]
    public async Task<IActionResult> Rate(RateTripCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
