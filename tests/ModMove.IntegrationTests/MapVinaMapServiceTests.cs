using System.Net;
using System.Text;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Exceptions;
using ModMove.Infrastructure.ExternalServices.MapVina;

namespace ModMove.IntegrationTests;

/// <summary>Parsing and error mapping of the MapVina adapter (DECISIONS 3.4), against canned JSON (no network).</summary>
public class MapVinaMapServiceTests
{
    private sealed class StubHandler(string json, HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private const string Key = "test-key";

    private static (MapVinaMapService Service, StubHandler Handler) Create(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(json, status);
        return (new MapVinaMapService(new HttpClient(handler), new MapVinaOptions { ApiKey = Key }), handler);
    }

    private static readonly GeoPoint HoanKiem = new(21.0285m, 105.8542m);
    private static readonly GeoPoint CauGiay = new(21.0362m, 105.7905m);

    [Fact]
    public async Task Geocode_reads_the_first_location()
    {
        var (maps, _) = Create("""
            {"status":"OK","results":[
              {"geometry":{"location":{"lat":21.0285,"lng":105.8542}}},
              {"geometry":{"location":{"lat":10.7769,"lng":106.7009}}}]}
            """);

        var point = await maps.GeocodeAsync("Hoàn Kiếm, Hà Nội", CancellationToken.None);

        Assert.Equal(HoanKiem, point);
    }

    [Fact]
    public async Task Geocode_searches_in_vietnamese_with_the_address_encoded()
    {
        var (maps, handler) = Create("""{"status":"OK","results":[{"geometry":{"location":{"lat":1,"lng":2}}}]}""");

        await maps.GeocodeAsync("123 Xuân Thủy, Cầu Giấy", CancellationToken.None);

        var uri = handler.LastRequest!.RequestUri!;
        Assert.Equal("/api/v2/place/textsearch/json", uri.AbsolutePath);
        Assert.Contains("language=vi", uri.Query);
        Assert.Contains("query=" + Uri.EscapeDataString("123 Xuân Thủy, Cầu Giấy"), uri.Query);
        Assert.Contains($"key={Key}", uri.Query);
    }

    [Theory]
    [InlineData("""{"status":"ZERO_RESULTS","results":[]}""")]
    [InlineData("""{"status":"OK","results":[]}""")]
    [InlineData("""{"status":"OK"}""")]
    public async Task Geocode_without_results_is_ADDRESS_NOT_FOUND(string json)
    {
        var (maps, _) = Create(json);

        var error = await Assert.ThrowsAsync<DomainException>(() => maps.GeocodeAsync("asdfgh", CancellationToken.None));

        Assert.Equal("ADDRESS_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Geocode_denied_is_a_server_error_without_the_key()
    {
        var (maps, _) = Create("""{"status":"REQUEST_DENIED","results":[]}""");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => maps.GeocodeAsync("Hà Nội", CancellationToken.None));

        Assert.DoesNotContain(Key, error.Message);
    }

    [Fact]
    public async Task Rejected_key_is_a_server_error_without_the_key()
    {
        var (maps, _) = Create("""{"message":"Unauthorized"}""", HttpStatusCode.Unauthorized);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => maps.GeocodeAsync("Hà Nội", CancellationToken.None));

        Assert.DoesNotContain(Key, error.Message);
    }

    [Fact]
    public async Task Route_converts_meters_and_seconds()
    {
        var (maps, _) = Create("""
            {"status":"OK","routes":[{"legs":[{"distance":{"text":"12.3 km","value":12345},"duration":{"text":"26 phút","value":1530}}]}]}
            """);

        var route = await maps.GetRouteAsync(HoanKiem, CauGiay, CancellationToken.None);

        Assert.Equal(new RouteInfo(12.35m, 26), route);
    }

    [Fact]
    public async Task Route_keeps_the_overview_polyline_for_the_driver_map()
    {
        var (maps, _) = Create("""
            {"status":"OK","routes":[{"overview_polyline":{"points":"abc~"},"legs":[{"distance":{"value":1000},"duration":{"value":60}}]}]}
            """);

        var route = await maps.GetRouteAsync(HoanKiem, CauGiay, CancellationToken.None);

        Assert.Equal(new RouteInfo(1m, 1, "abc~"), route);
    }

    [Fact]
    public async Task Route_adds_up_every_leg()
    {
        var (maps, _) = Create("""
            {"status":"OK","routes":[{"legs":[
              {"distance":{"value":1000},"duration":{"value":60}},
              {"distance":{"value":2500},"duration":{"value":90}}]}]}
            """);

        var route = await maps.GetRouteAsync(HoanKiem, CauGiay, CancellationToken.None);

        Assert.Equal(new RouteInfo(3.5m, 3), route);
    }

    [Theory]
    [InlineData("""{"status":"REQUEST_DENIED","routes":[]}""")]
    [InlineData("""{"status":"OVER_QUERY_LIMIT","routes":[]}""")]
    public async Task Route_denied_or_over_quota_is_a_server_error_not_ROUTE_NOT_FOUND(string json)
    {
        var (maps, _) = Create(json);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => maps.GetRouteAsync(HoanKiem, CauGiay, CancellationToken.None));

        Assert.DoesNotContain(Key, error.Message);
    }

    [Fact]
    public async Task Same_pickup_and_dropoff_is_a_zero_km_route()
    {
        var (maps, _) = Create("""
            {"status":"OK","routes":[{"legs":[{"distance":{"text":"0m","value":0},"duration":{"text":"0.0 giây","value":0}}]}]}
            """);

        var route = await maps.GetRouteAsync(HoanKiem, HoanKiem, CancellationToken.None);

        Assert.Equal(new RouteInfo(0m, 0), route);
    }

    [Theory]
    [InlineData("""{"status":"ZERO_RESULTS","routes":[]}""")]
    [InlineData("""{"status":"NOT_FOUND"}""")]
    [InlineData("""{"status":"OK","routes":[]}""")]
    public async Task Route_without_routes_is_ROUTE_NOT_FOUND(string json)
    {
        var (maps, _) = Create(json);

        var error = await Assert.ThrowsAsync<DomainException>(() => maps.GetRouteAsync(HoanKiem, CauGiay, CancellationToken.None));

        Assert.Equal("ROUTE_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Route_sends_lat_lng_with_invariant_decimals_by_car()
    {
        var (maps, handler) = Create("""
            {"status":"OK","routes":[{"legs":[{"distance":{"value":1000},"duration":{"value":60}}]}]}
            """);
        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("vi-VN"); // decimal comma
        try
        {
            await maps.GetRouteAsync(HoanKiem, CauGiay, CancellationToken.None);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }

        var uri = handler.LastRequest!.RequestUri!;
        Assert.Equal("/route/v2/directions/json", uri.AbsolutePath);
        var query = Uri.UnescapeDataString(uri.Query);
        Assert.Contains("origin=21.0285,105.8542", query);
        Assert.Contains("destination=21.0362,105.7905", query);
        Assert.Contains("mode=driving", query);
        Assert.Contains($"key={Key}", query);
    }
}
