using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Drivers.Queries.GetDriverRoute;

internal sealed class GetDriverRouteQueryHandler(IMapService maps) : IQueryHandler<GetDriverRouteQuery, DriverRouteResponse>
{
    public async Task<DriverRouteResponse> Handle(GetDriverRouteQuery request, CancellationToken cancellationToken)
    {
        var route = await maps.GetRouteAsync(
            new GeoPoint(request.FromLatitude, request.FromLongitude),
            new GeoPoint(request.ToLatitude, request.ToLongitude),
            cancellationToken);
        return new DriverRouteResponse(route.DistanceKm, route.DurationMinutes, route.Polyline);
    }
}
