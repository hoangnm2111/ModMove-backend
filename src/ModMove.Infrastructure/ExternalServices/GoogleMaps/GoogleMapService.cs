using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Exceptions;

namespace ModMove.Infrastructure.ExternalServices.GoogleMaps;

/// <summary>
/// DECISIONS 3.4: Geocoding API for addresses and Routes API (computeRoutes) for the distance that feeds the fare.
/// Distance Matrix cannot be enabled on new projects, so it is not used.
/// </summary>
public sealed class GoogleMapService(HttpClient http, GoogleMapsOptions options) : IMapService
{
    private const string GeocodeUrl = "https://maps.googleapis.com/maps/api/geocode/json";
    private const string RoutesUrl = "https://routes.googleapis.com/directions/v2:computeRoutes";

    public async Task<GeoPoint> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        var url = $"{GeocodeUrl}?address={Uri.EscapeDataString(address)}&region=vn&language=vi&key={Uri.EscapeDataString(options.ApiKey)}";
        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var status = json.RootElement.GetProperty("status").GetString();
        if (status == "ZERO_RESULTS")
            throw new DomainException("Address not found.", "ADDRESS_NOT_FOUND");
        if (status != "OK")
            throw new InvalidOperationException($"Google Geocoding failed with status {status}."); // the key is in the URL: never log the URL

        var location = json.RootElement.GetProperty("results")[0].GetProperty("geometry").GetProperty("location");
        return new GeoPoint(location.GetProperty("lat").GetDecimal(), location.GetProperty("lng").GetDecimal());
    }

    public async Task<RouteInfo> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, RoutesUrl)
        {
            Content = JsonContent.Create(new
            {
                origin = Waypoint(from),
                destination = Waypoint(to),
                travelMode = "DRIVE",
            }),
        };
        request.Headers.Add("X-Goog-Api-Key", options.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", "routes.distanceMeters,routes.duration");

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        if (!json.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
            throw new DomainException("No road route between the two points.", "ROUTE_NOT_FOUND");

        // Routes API uses proto3 JSON, which leaves out zero values: same pickup and dropoff has no distanceMeters.
        var route = routes[0];
        var meters = route.TryGetProperty("distanceMeters", out var distance) ? distance.GetDecimal() : 0m;
        var seconds = route.TryGetProperty("duration", out var duration)
            ? decimal.Parse(duration.GetString()!.TrimEnd('s'), CultureInfo.InvariantCulture)
            : 0m;
        return new RouteInfo(Math.Round(meters / 1000m, 2, MidpointRounding.AwayFromZero), (int)Math.Ceiling(seconds / 60m));
    }

    private static object Waypoint(GeoPoint point) =>
        new { location = new { latLng = new { latitude = point.Latitude, longitude = point.Longitude } } };
}
