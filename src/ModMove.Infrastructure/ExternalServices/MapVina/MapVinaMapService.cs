using System.Globalization;
using System.Text.Json;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Exceptions;

namespace ModMove.Infrastructure.ExternalServices.MapVina;

/// <summary>
/// DECISIONS 3.4: MapVina Search v2 (Google-compatible text search) turns an address into coordinates and
/// Directions v2 gives the driving distance that feeds the fare. The key travels in the query string, so never log a URL.
/// </summary>
public sealed class MapVinaMapService(HttpClient http, MapVinaOptions options) : IMapService
{
    private const string SearchUrl = "https://maps.mapvina.com/api/v2/place/textsearch/json";
    private const string DirectionsUrl = "https://maps.mapvina.com/route/v2/directions/json";

    public async Task<GeoPoint> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        using var json = await GetAsync($"{SearchUrl}?language=vi&query={Uri.EscapeDataString(address)}", cancellationToken);

        var status = json.RootElement.GetProperty("status").GetString();
        var results = json.RootElement.TryGetProperty("results", out var r) && r.ValueKind == JsonValueKind.Array ? r : default;
        if (status == "ZERO_RESULTS" || (status == "OK" && (results.ValueKind == JsonValueKind.Undefined || results.GetArrayLength() == 0)))
            throw new DomainException("Address not found.", "ADDRESS_NOT_FOUND");
        if (status != "OK")
            throw new InvalidOperationException($"MapVina search failed with status {status}.");

        var location = results[0].GetProperty("geometry").GetProperty("location");
        return new GeoPoint(location.GetProperty("lat").GetDecimal(), location.GetProperty("lng").GetDecimal());
    }

    public async Task<RouteInfo> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken)
    {
        using var json = await GetAsync(
            $"{DirectionsUrl}?origin={LatLng(from)}&destination={LatLng(to)}&mode=driving", cancellationToken);

        var status = json.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
        var hasRoute = json.RootElement.TryGetProperty("routes", out var routes) && routes.ValueKind == JsonValueKind.Array
            && routes.GetArrayLength() > 0;
        if (status is "ZERO_RESULTS" or "NOT_FOUND" || (status == "OK" && !hasRoute))
            throw new DomainException("No road route between the two points.", "ROUTE_NOT_FOUND");
        if (status != "OK")
            throw new InvalidOperationException($"MapVina directions failed with status {status}."); // e.g. denied key or used-up quota

        decimal meters = 0, seconds = 0;
        foreach (var leg in routes[0].GetProperty("legs").EnumerateArray())
        {
            meters += leg.GetProperty("distance").GetProperty("value").GetDecimal();
            seconds += leg.GetProperty("duration").GetProperty("value").GetDecimal();
        }
        return new RouteInfo(Math.Round(meters / 1000m, 2, MidpointRounding.AwayFromZero), (int)Math.Ceiling(seconds / 60m));
    }

    private async Task<JsonDocument> GetAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"{url}&key={Uri.EscapeDataString(options.ApiKey)}", cancellationToken);
        response.EnsureSuccessStatusCode(); // its message has the status code only, not the URL with the key
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static string LatLng(GeoPoint point) =>
        string.Create(CultureInfo.InvariantCulture, $"{point.Latitude},{point.Longitude}");
}
