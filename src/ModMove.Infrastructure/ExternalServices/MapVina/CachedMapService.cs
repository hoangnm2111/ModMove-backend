using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.MapVina;

/// <summary>
/// Keeps MapVina answers in memory. The Free plan has 200 geocodes and 200 routes a day (DECISIONS 3.4), and a fare
/// estimate followed by the order for the same addresses then costs one lookup and gets the same km twice (4.4).
/// Only successful answers are kept: a denied key or an outage must not be remembered.
/// </summary>
public sealed class CachedMapService(IMapService inner, IMemoryCache cache) : IMapService
{
    public static readonly TimeSpan GeocodeTtl = TimeSpan.FromDays(7); // an address does not move
    public static readonly TimeSpan RouteTtl = TimeSpan.FromDays(1);   // driving route without traffic, stable

    public async Task<GeoPoint> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        // Case and spaces do not change the place; diacritics do ("Lê Lợi" and "Le Loi" can be different streets).
        var key = "mapvina:geo:" + string.Join(' ', address.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
        if (cache.TryGetValue(key, out GeoPoint? hit))
            return hit!;

        var point = await inner.GeocodeAsync(address, cancellationToken);
        cache.Set(key, point, GeocodeTtl);
        return point;
    }

    public async Task<RouteInfo> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken)
    {
        var key = $"mapvina:route:{LatLng(from)}:{LatLng(to)}";
        if (cache.TryGetValue(key, out RouteInfo? hit))
            return hit!;

        var route = await inner.GetRouteAsync(from, to, cancellationToken);
        cache.Set(key, route, RouteTtl);
        return route;
    }

    // 5 decimals is about 1 m; invariant culture so the key never depends on the server's decimal separator.
    private static string LatLng(GeoPoint point) => string.Create(CultureInfo.InvariantCulture,
        $"{Math.Round(point.Latitude, 5, MidpointRounding.AwayFromZero)},{Math.Round(point.Longitude, 5, MidpointRounding.AwayFromZero)}");
}
