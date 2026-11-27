using ModMove.Domain.Enums;

namespace ModMove.Domain.Constants;

/// <summary>Shared groupings of <see cref="OrderStatus"/> used across repositories and queries.</summary>
public static class OrderStatusGroups
{
    /// <summary>
    /// Orders currently held by a driver: Accepted up to before Delivered (DECISIONS C8, GB-09).
    /// Stored as a list rather than a range because enums are persisted as text.
    /// </summary>
    public static readonly OrderStatus[] WithDriver =
        [OrderStatus.Accepted, OrderStatus.Arrived, OrderStatus.PickedUp, OrderStatus.InTransit];
}
