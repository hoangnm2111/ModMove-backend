using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;

/// <summary>
/// DECISIONS B12, behind REST POST api/driver/location and SendLocation on /hubs/tracking: keeps the driver's last position,
/// logs it only while the driver has an order (at most once a minute), and pushes LocationUpdated to the order's followers and
/// (for an Online driver) to the Dispatcher map. <see cref="DriverUserId"/> comes from the token, never from the client's message.
/// <see cref="Accuracy"/> in meters (B19), <see cref="IsMocked"/> (B20) and <see cref="RecordedAt"/> for backlog points (B21) are
/// optional, so older clients keep working.
/// </summary>
public sealed record UpdateDriverLocationCommand(
    long DriverUserId,
    decimal Latitude,
    decimal Longitude,
    decimal? Accuracy = null,
    bool IsMocked = false,
    DateTimeOffset? RecordedAt = null) : ICommand;

/// <summary>Payload of the LocationUpdated event.</summary>
public sealed record DriverLocationUpdate(long DriverProfileId, long? OrderId, decimal Latitude, decimal Longitude, DateTimeOffset At);
