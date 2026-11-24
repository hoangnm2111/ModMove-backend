using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using ModMove.Infrastructure.ExternalServices.MapVina;

namespace ModMove.IntegrationTests;

/// <summary>The browser gets MapVina maps through the server, so the MapVina key never reaches it (DECISIONS 3.4).</summary>
public class MapVinaProxyTests
{
    private const string Proxy = "https://api.example.test/api/public/Map";
    private const string Secret = "secret-maps-key";

    private sealed class Upstream(HttpStatusCode status, string contentType, string body) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) });
        }
    }

    private static MapVinaProxy Create(Upstream upstream) =>
        new(new HttpClient(upstream), new MapVinaOptions { ApiKey = Secret }, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void Rewrite_points_every_url_at_the_server_and_drops_the_key()
    {
        const string style = """
            {"sprite":"https://maps.mapvina.com/mapstyle/sprite?key=public_key",
             "glyphs":"https://maps.mapvina.com/mapstyle/font/{fontstack}/{range}.pbf?key=public_key",
             "sources":{"base":{"type":"vector","url":"https://maps.mapvina.com/tiles/v2/composite.json?key=public_key"}},
             "tiles":["https://tiles.mapvina.com/tiles/v2/{z}/{x}/{y}.pbf?key=public_key&x=1"]}
            """;

        var result = MapVinaProxy.Rewrite(style, Proxy + "/");

        Assert.DoesNotContain("key=", result);
        Assert.DoesNotContain("mapvina.com", result);
        Assert.Contains($"{Proxy}/maps/mapstyle/sprite\"", result);
        Assert.Contains($"{Proxy}/maps/tiles/v2/composite.json\"", result);
        Assert.Contains($"{Proxy}/tiles/tiles/v2/{{z}}/{{x}}/{{y}}.pbf?x=1\"", result);
    }

    [Theory]
    [InlineData("styles/v2/streets.json", true)]
    [InlineData("mapstyle/font/Noto%20Sans/0-255.pbf", true)]
    [InlineData("api/v2/place/textsearch/json", false)] // geocoding spends the shared quota: not a map file
    [InlineData("route/v2/directions/json", false)]
    [InlineData("styles/../route/v2/directions/json", false)]
    [InlineData("styles//streets.json", false)]
    [InlineData("styles\\streets.json", false)]
    [InlineData("", false)]
    public void Only_map_files_can_be_fetched(string path, bool allowed) =>
        Assert.Equal(allowed, MapVinaProxy.IsAllowedPath(path, ["styles/", "mapstyle/", "tiles/"]));

    [Fact]
    public async Task The_server_adds_its_own_key_and_ignores_the_one_sent_by_the_client()
    {
        var upstream = new Upstream(HttpStatusCode.OK, "application/json", """{"tiles":["https://tiles.mapvina.com/tiles/v2/{z}/{x}/{y}.pbf?key=public_key"]}""");

        var file = await Create(upstream).GetResourceAsync("maps", "tiles/v2/composite.json", "?key=attacker&x=1", Proxy, CancellationToken.None);

        Assert.Equal($"https://maps.mapvina.com/tiles/v2/composite.json?x=1&key={Secret}", Assert.Single(upstream.Urls));
        var body = Encoding.UTF8.GetString(file!.Content);
        Assert.DoesNotContain(Secret, body);
        Assert.DoesNotContain("key=", body);
        Assert.Contains($"{Proxy}/tiles/tiles/v2/", body);
    }

    [Fact]
    public async Task Small_files_are_kept_but_tiles_are_not()
    {
        var upstream = new Upstream(HttpStatusCode.OK, "application/x-protobuf", "x");
        var proxy = Create(upstream);

        await proxy.GetResourceAsync("maps", "mapstyle/font/A/0-255.pbf", "", Proxy, CancellationToken.None);
        await proxy.GetResourceAsync("maps", "mapstyle/font/A/0-255.pbf", "", Proxy, CancellationToken.None);
        Assert.Single(upstream.Urls);

        await proxy.GetResourceAsync("tiles", "tiles/v2/1/2/3.pbf", "", Proxy, CancellationToken.None);
        await proxy.GetResourceAsync("tiles", "tiles/v2/1/2/3.pbf", "", Proxy, CancellationToken.None);
        Assert.Equal(3, upstream.Urls.Count);
    }

    [Fact]
    public async Task A_missing_tile_is_a_404_and_a_failure_never_shows_the_key()
    {
        Assert.Null(await Create(new Upstream(HttpStatusCode.NotFound, "text/plain", "")).GetResourceAsync("tiles", "tiles/v2/9/9/9.pbf", "", Proxy, CancellationToken.None));
        Assert.Null(await Create(new Upstream(HttpStatusCode.BadRequest, "text/plain", "")).GetResourceAsync("tiles", "tiles/v2/13/1/1.pbf", "", Proxy, CancellationToken.None));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create(new Upstream(HttpStatusCode.Forbidden, "text/plain", "")).GetResourceAsync("maps", "styles/v2/streets.json", "", Proxy, CancellationToken.None));
        Assert.DoesNotContain(Secret, ex.Message);
    }

    [Fact]
    public async Task Unknown_hosts_and_paths_are_not_fetched()
    {
        var upstream = new Upstream(HttpStatusCode.OK, "application/json", "{}");
        var proxy = Create(upstream);

        Assert.Null(await proxy.GetResourceAsync("evil", "styles/v2/streets.json", "", Proxy, CancellationToken.None));
        Assert.Null(await proxy.GetResourceAsync("maps", "api/v2/place/textsearch/json", "", Proxy, CancellationToken.None));
        Assert.Empty(upstream.Urls);
    }
}
