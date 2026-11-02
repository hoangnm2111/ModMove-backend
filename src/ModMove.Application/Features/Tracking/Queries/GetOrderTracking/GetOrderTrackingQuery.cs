using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Tracking.Queries.GetOrderTracking;

/// <summary>
/// UC-CUS-13: where the order is now, read when the tracking screen opens or reconnects; after that the client follows
/// LocationUpdated on /hubs/tracking (B12). Someone else's order is 404.
/// </summary>
public sealed record GetOrderTrackingQuery(long OrderId) : IQuery<OrderTrackingResponse>;

/// <summary><see cref="Driver"/> is null outside Accepted..InTransit or before the first GPS point.</summary>
public sealed record OrderTrackingResponse(
    long OrderId, string OrderStatus, GeoPoint Pickup, GeoPoint Dropoff, DriverPositionResponse? Driver, TrackingEtaResponse? Eta);

/// <summary><see cref="IsStale"/>: no GPS for over 5 minutes, the driver counts as disconnected (6.4).</summary>
public sealed record DriverPositionResponse(decimal Latitude, decimal Longitude, DateTimeOffset At, bool IsStale);

/// <summary>
/// <see cref="Target"/>: Pickup, Handover (the old vehicle, after a mid-trip reassignment, 7.2) or Dropoff. Null on the response
/// when the position is stale, the handover location is unknown, or Google Maps failed or took over 3 seconds.
/// </summary>
public sealed record TrackingEtaResponse(string Target, int DurationMinutes);
