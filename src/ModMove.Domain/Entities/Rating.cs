using ModMove.Domain.Common;
using ModMove.Domain.Constants;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>The customer's rating of a completed trip. One per order and never edited (GB-20).</summary>
public sealed class Rating : BaseCreatedEntity, IAggregateRoot
{
    private Rating() { }

    public long OrderId { get; private set; }
    public long CustomerId { get; private set; }
    public long DriverProfileId { get; private set; }
    public int RatingStars { get; private set; }
    public string? ReviewComment { get; private set; }

    /// <summary>
    /// GB-20, DECISIONS 7.5: only for a completed order, within 1 day of its completion.
    /// <paramref name="orderCompletedAt"/> is null when the order is not completed.
    /// </summary>
    public static Rating Create(
        long orderId, long customerId, long driverProfileId, int stars, string? comment,
        DateTimeOffset? orderCompletedAt, DateTimeOffset now)
    {
        if (orderCompletedAt is null)
            throw new DomainException("Only a completed trip can be rated.", "RATING_NOT_ALLOWED");
        if (now - orderCompletedAt > BusinessRules.RatingWindow)
            throw new DomainException("A trip can be rated only within 1 day of completion.", "RATING_WINDOW_CLOSED");
        if (stars is < 1 or > 5)
            throw new DomainException("A rating must be between 1 and 5 stars.");

        return new Rating
        {
            OrderId = orderId,
            CustomerId = customerId,
            DriverProfileId = driverProfileId,
            RatingStars = stars,
            ReviewComment = comment,
        };
    }
}
