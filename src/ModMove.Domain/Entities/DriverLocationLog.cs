using ModMove.Domain.Common;

namespace ModMove.Domain.Entities;

/// <summary>GPS point of a driver who has an order, at most one per minute per order (B12). Purged after 30 days (JOB-GPS-01).</summary>
public sealed class DriverLocationLog : BaseCreatedEntity, IAggregateRoot
{
    private DriverLocationLog() { }

    public long DriverProfileId { get; private set; }
    public long? OrderId { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public static DriverLocationLog Record(long driverProfileId, long? orderId, decimal latitude, decimal longitude, DateTimeOffset recordedAt) =>
        new()
        {
            DriverProfileId = driverProfileId,
            OrderId = orderId,
            Latitude = latitude,
            Longitude = longitude,
            RecordedAt = recordedAt,
        };
}
