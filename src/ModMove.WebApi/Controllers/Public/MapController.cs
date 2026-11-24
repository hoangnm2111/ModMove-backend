using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Interfaces;

namespace ModMove.WebApi.Controllers.Public;

/// <summary>
/// MapVina map for the web without the MapVina key in the browser (DECISIONS 3.4). Point MapLibre at <c>GET /api/public/Map/style</c>;
/// every URL inside the style comes back to this controller, which adds the key on the server.
/// Anonymous like the landing page; only MapVina style, sprite, glyph and tile files can be fetched.
/// </summary>
public sealed class MapController(IMapProxy proxy) : ApiControllerBase
{
    /// <summary>MapVina street style (MapLibre style spec) with every URL pointing back here.</summary>
    [HttpGet("style")]
    public async Task<IActionResult> Style(CancellationToken cancellationToken) =>
        Serve(proxy.IsConfigured ? await proxy.GetStyleAsync(BaseUrl, cancellationToken) : null);

    /// <summary>Style, TileJSON, sprite and glyph files of maps.mapvina.com.</summary>
    [HttpGet("maps/{**path}")]
    public Task<IActionResult> Maps(string path, CancellationToken cancellationToken) => Resource("maps", path, cancellationToken);

    /// <summary>Vector tiles of tiles.mapvina.com.</summary>
    [HttpGet("tiles/{**path}")]
    public Task<IActionResult> Tiles(string path, CancellationToken cancellationToken) => Resource("tiles", path, cancellationToken);

    private async Task<IActionResult> Resource(string host, string path, CancellationToken cancellationToken) =>
        Serve(proxy.IsConfigured
            ? await proxy.GetResourceAsync(host, path, Request.QueryString.Value ?? "", BaseUrl, cancellationToken)
            : null);

    private string BaseUrl => $"{Request.Scheme}://{Request.Host}/api/public/Map";

    private IActionResult Serve(MapProxyResponse? file)
    {
        if (!proxy.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (file is null)
            return NotFound();
        Response.Headers.CacheControl = $"public, max-age={(int)file.MaxAge.TotalSeconds}";
        return File(file.Content, file.ContentType);
    }
}
