using ModMove.Domain.Common;
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

    public static Rating Create(long orderId, long customerId, long driverProfileId, int stars, string? comment)
    {
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
