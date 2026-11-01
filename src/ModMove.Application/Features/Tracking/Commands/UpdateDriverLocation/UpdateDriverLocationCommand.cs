using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;

/// <summary>
/// DECISIONS B12, behind SendLocation on /hubs/tracking: keeps the driver's last position, logs it only while the driver
/// has an order, and pushes LocationUpdated to the order's followers and (for an Online driver) to the Dispatcher map.
/// <see cref="DriverUserId"/> comes from the hub connection's token, never from the client's message.
/// </summary>
public sealed record UpdateDriverLocationCommand(long DriverUserId, decimal Latitude, decimal Longitude) : ICommand;

/// <summary>Payload of the LocationUpdated event.</summary>
public sealed record DriverLocationUpdate(long DriverProfileId, long? OrderId, decimal Latitude, decimal Longitude, DateTimeOffset At);
