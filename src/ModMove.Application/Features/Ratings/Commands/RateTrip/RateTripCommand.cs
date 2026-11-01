using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Ratings.Commands.RateTrip;

/// <summary>UC-CUS-25: 1-5 stars and an optional comment for the driver of a completed trip, once (GB-20), within 1 day (7.5).</summary>
public sealed record RateTripCommand(long OrderId, int Stars, string? Comment) : ICommand<RateTripResponse>;

public sealed record RateTripResponse(long RatingId);
