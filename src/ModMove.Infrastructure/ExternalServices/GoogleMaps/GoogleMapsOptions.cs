namespace ModMove.Infrastructure.ExternalServices.GoogleMaps;

/// <summary>Bound from the "GoogleMaps" section; the key lives in appsettings.Development.json / user secrets, never in the repo.</summary>
public sealed class GoogleMapsOptions
{
    /// <summary>Restricted to Geocoding API and Routes API in the Google Cloud console.</summary>
    public string ApiKey { get; init; } = "";
}
