namespace ModMove.Infrastructure.ExternalServices.MapVina;

/// <summary>Bound from the "MapVina" section; the key lives in appsettings.Development.json / user secrets, never in the repo.</summary>
public sealed class MapVinaOptions
{
    public string ApiKey { get; init; } = "";
}
