using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.MapVina;

/// <summary>
/// Stands in when MapVina:ApiKey is not set, so the app still starts (Development validates every handler at
/// startup) and teammates without the key can work on other features. Fare estimate and order creation fail with a
/// clear server error until the key is configured.
/// </summary>
public sealed class NotConfiguredMapService : IMapService
{
    private const string Message = "MapVina is not configured: set MapVina:ApiKey in appsettings.Development.json.";

    public Task<GeoPoint> GeocodeAsync(string address, CancellationToken cancellationToken) => throw new InvalidOperationException(Message);

    public Task<RouteInfo> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken) => throw new InvalidOperationException(Message);
}
