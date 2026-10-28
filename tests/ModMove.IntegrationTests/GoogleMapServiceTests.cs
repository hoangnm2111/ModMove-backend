using System.Net;
using System.Text;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Exceptions;
using ModMove.Infrastructure.ExternalServices.GoogleMaps;

namespace ModMove.IntegrationTests;

/// <summary>Parsing and error mapping of the Google Maps adapter, against canned JSON (no network).</summary>
public class GoogleMapServiceTests
{
    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (GoogleMapService Service, StubHandler Handler) Create(string json)
    {
        var handler = new StubHandler(json);
        return (new GoogleMapService(new HttpClient(handler), new GoogleMapsOptions { ApiKey = "test-key" }), handler);
    }

    private static readonly GeoPoint HoanKiem = new(21.0285m, 105.8542m);
    private static readonly GeoPoint CauGiay = new(21.0362m, 105.7905m);

    [Fact]
    public async Task Geocode_reads_the_first_location()
    {
        var (maps, _) = Create("""{"status":"OK","results":[{"geometry":{"location":{"lat":21.0285,"lng":105.8542}}}]}""");

        var point = await maps.GeocodeAsync("Hoàn Kiếm, Hà Nội", CancellationToken.None);

        Assert.Equal(HoanKiem, point);
    }

    [Fact]
    public async Task Geocode_zero_results_is_ADDRESS_NOT_FOUND()
    {
        var (maps, _) = Create("""{"status":"ZERO_RESULTS","results":[]}""");

        var error = await Assert.ThrowsAsync<DomainException>(() => maps.GeocodeAsync("zzzz", CancellationToken.None));

        Assert.Equal("ADDRESS_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Route_converts_meters_and_seconds()
    {
        var (maps, _) = Create("""{"routes":[{"distanceMeters":12345,"duration":"1530s"}]}""");

        var route = await maps.GetRouteAsync(HoanKiem, CauGiay, CancellationToken.None);

        Assert.Equal(new RouteInfo(12.35m, 26), route);
    }

    [Fact]
    public async Task Route_without_routes_is_ROUTE_NOT_FOUND()
    {
        var (maps, _) = Create("{}");

        var error = await Assert.ThrowsAsync<DomainException>(() => maps.GetRouteAsync(HoanKiem, CauGiay, CancellationToken.None));

        Assert.Equal("ROUTE_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Route_sends_key_in_header_not_in_url()
    {
        var (maps, handler) = Create("""{"routes":[{"distanceMeters":1000,"duration":"60s"}]}""");

        await maps.GetRouteAsync(HoanKiem, CauGiay, CancellationToken.None);

        Assert.Equal("test-key", handler.LastRequest!.Headers.GetValues("X-Goog-Api-Key").Single());
        Assert.DoesNotContain("test-key", handler.LastRequest.RequestUri!.ToString());
    }
}
