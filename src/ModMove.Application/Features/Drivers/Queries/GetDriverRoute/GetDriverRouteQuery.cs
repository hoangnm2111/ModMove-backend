using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Drivers.Queries.GetDriverRoute;

/// <summary>
/// Driver app map: the driving route from the driver's position to the pickup point. The app calls this instead of
/// MapVina so the MapVina key for routes stays on the server and answers are cached (DECISIONS 3.4).
/// </summary>
public sealed record GetDriverRouteQuery(decimal? FromLatitude, decimal? FromLongitude, decimal? ToLatitude, decimal? ToLongitude)
    : IQuery<DriverRouteResponse>;

/// <param name="Polyline">Encoded polyline with 5 decimals (MapVina, same as Google); null when MapVina sent no shape, and the app draws a straight line.</param>
public sealed record DriverRouteResponse(decimal DistanceKm, int DurationMinutes, string? Polyline);
