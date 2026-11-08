using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Ratings.Queries.GetDriverRatings;

/// <summary>
/// DECISIONS 7.5, 8.3: rating summary (average stars and star distribution calculated on query) and paginated reviews
/// received by the signed-in driver, newest first.
/// </summary>
public sealed record GetDriverRatingsQuery(
    int Page = 1,
    int PageSize = 20,
    int? Stars = null) : IQuery<DriverRatingsResponse>;

public sealed record DriverRatingsResponse(
    decimal? AverageStars,
    int TotalRatings,
    int FiveStarCount,
    int FourStarCount,
    int ThreeStarCount,
    int TwoStarCount,
    int OneStarCount,
    PagedResult<DriverRatingItemResponse> Reviews);

public sealed record DriverRatingItemResponse(
    long Id,
    long OrderId,
    string OrderCode,
    int RatingStars,
    string? ReviewComment,
    string CustomerName,
    DateTimeOffset CreatedAt);

