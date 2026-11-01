using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Ratings.Queries.GetMyRatings;

/// <summary>UC-CUS-26: the ratings the signed-in customer has given, newest first.</summary>
public sealed record GetMyRatingsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<MyRatingResponse>>;

public sealed record MyRatingResponse(
    long Id, long OrderId, string OrderCode, int RatingStars, string? ReviewComment, string DriverName, DateTimeOffset CreatedAt);
