using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.MapVina;

/// <summary>
/// Serves MapVina's map files to the browser with the key added on the server. JSON answers (style, TileJSON) are rewritten so
/// every URL in them points back here and no longer carries <c>key=</c>; sprites, glyphs and vector tiles are passed through.
/// Small files are kept in memory; vector tiles are not (the browser caches them for a day).
/// </summary>
public sealed partial class MapVinaProxy(HttpClient http, MapVinaOptions options, IMemoryCache cache) : IMapProxy
{
    private const string MapsOrigin = "https://maps.mapvina.com";
    private const string TilesOrigin = "https://tiles.mapvina.com";
    private const string StylePath = "styles/v2/streets.json";

    public static readonly TimeSpan StyleMaxAge = TimeSpan.FromHours(1);
    public static readonly TimeSpan JsonMaxAge = TimeSpan.FromHours(6);
    public static readonly TimeSpan BinaryMaxAge = TimeSpan.FromDays(1);

    private static readonly Dictionary<string, (string Origin, string[] Prefixes)> Hosts = new(StringComparer.Ordinal)
    {
        ["maps"] = (MapsOrigin, ["styles/", "mapstyle/", "tiles/"]),
        ["tiles"] = (TilesOrigin, ["tiles/"]),
    };

    public bool IsConfigured => true;

    public async Task<MapProxyResponse> GetStyleAsync(string proxyBaseUrl, CancellationToken cancellationToken) =>
        (await FetchAsync("maps", StylePath, "", proxyBaseUrl, StyleMaxAge, cancellationToken))!;

    public Task<MapProxyResponse?> GetResourceAsync(string host, string path, string query, string proxyBaseUrl, CancellationToken cancellationToken) =>
        FetchAsync(host, path, query, proxyBaseUrl, null, cancellationToken);

    private async Task<MapProxyResponse?> FetchAsync(
        string host, string path, string query, string proxyBaseUrl, TimeSpan? maxAgeOverride, CancellationToken cancellationToken)
    {
        if (!Hosts.TryGetValue(host, out var target) || !IsAllowedPath(path, target.Prefixes))
            return null;

        var rest = StripKey(query);
        var upstream = $"{target.Origin}/{string.Join('/', path.Split('/').Select(Uri.EscapeDataString))}";
        var cacheKey = $"mapvina:proxy:{proxyBaseUrl}:{host}:{upstream}?{rest}";
        if (cache.TryGetValue(cacheKey, out MapProxyResponse? hit))
            return hit;

        var keyed = $"{upstream}?{(rest.Length > 0 ? rest + "&" : "")}key={Uri.EscapeDataString(options.ApiKey)}";
        using var response = await http.GetAsync(keyed, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest)
            return null; // e.g. a tile outside MapVina's data or a bad z/x/y: the map just leaves it empty
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"MapVina map file failed with status {(int)response.StatusCode}."); // never the URL: it carries the key
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

        var isJson = contentType.Contains("json", StringComparison.OrdinalIgnoreCase);
        if (isJson)
            bytes = Encoding.UTF8.GetBytes(Rewrite(Encoding.UTF8.GetString(bytes), proxyBaseUrl));

        var maxAge = maxAgeOverride ?? (isJson ? JsonMaxAge : BinaryMaxAge);
        var result = new MapProxyResponse(bytes, contentType, maxAge);
        // Vector tiles are many; the browser cache is enough for them. Everything else (style, TileJSON, sprite, glyphs) is small.
        if (isJson || !path.StartsWith("tiles/v2/", StringComparison.Ordinal))
            cache.Set(cacheKey, result, maxAge);
        return result;
    }

    /// <summary>Only plain relative paths under an allowed prefix: no "..", no backslash, no empty segment.</summary>
    public static bool IsAllowedPath(string path, string[] prefixes)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 300 || path.Contains('\\') || path.Contains('\0'))
            return false;
        var segments = path.Split('/');
        if (segments.Any(s => s.Length == 0 || s == "." || s == ".."))
            return false;
        return prefixes.Any(p => path.StartsWith(p, StringComparison.Ordinal));
    }

    /// <summary>Points every MapVina URL in a JSON answer at this server and removes the key from it.</summary>
    public static string Rewrite(string json, string proxyBaseUrl)
    {
        var baseUrl = proxyBaseUrl.TrimEnd('/');
        var s = json
            .Replace(MapsOrigin + "/", baseUrl + "/maps/", StringComparison.Ordinal)
            .Replace(TilesOrigin + "/", baseUrl + "/tiles/", StringComparison.Ordinal);
        s = KeyThenMoreParams().Replace(s, "?");
        return KeyParam().Replace(s, "");
    }

    /// <summary>The incoming query without any <c>key</c> the client might send: the server's key is the only one used.</summary>
    private static string StripKey(string query) =>
        string.Join('&', query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("key=", StringComparison.OrdinalIgnoreCase)));

    [GeneratedRegex(@"\?key=[^""&\\]*(&|\\u0026)")]
    private static partial Regex KeyThenMoreParams();

    [GeneratedRegex(@"[?&]key=[^""&\\]*")]
    private static partial Regex KeyParam();
}

/// <summary>Without <c>MapVina:ApiKey</c> the map endpoints answer 503; the rest of the API still runs.</summary>
public sealed class NotConfiguredMapProxy : IMapProxy
{
    public bool IsConfigured => false;

    public Task<MapProxyResponse> GetStyleAsync(string proxyBaseUrl, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("MapVina:ApiKey is not set.");

    public Task<MapProxyResponse?> GetResourceAsync(string host, string path, string query, string proxyBaseUrl, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("MapVina:ApiKey is not set.");
}
