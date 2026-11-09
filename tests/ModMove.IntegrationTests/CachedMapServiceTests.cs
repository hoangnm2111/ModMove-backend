using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Exceptions;
using ModMove.Infrastructure.ExternalServices.MapVina;

namespace ModMove.IntegrationTests;

/// <summary>The cache in front of MapVina (DECISIONS 3.4 quota, 4.4 same km for estimate and order).</summary>
public class CachedMapServiceTests
{
    private sealed class Clock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);
    }

    /// <summary>Counts calls; each address gets its own point; queued exceptions are thrown first, one per call.</summary>
    private sealed class CountingMaps : IMapService
    {
        public int GeocodeCalls { get; private set; }
        public int RouteCalls { get; private set; }
        public Queue<Exception> Failures { get; } = new();

        public Task<GeoPoint> GeocodeAsync(string address, CancellationToken cancellationToken)
        {
            GeocodeCalls++;
            if (Failures.TryDequeue(out var failure))
                throw failure;
            return Task.FromResult(new GeoPoint(address.Length, GeocodeCalls));
        }

        public Task<RouteInfo> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken)
        {
            RouteCalls++;
            if (Failures.TryDequeue(out var failure))
                throw failure;
            return Task.FromResult(new RouteInfo(7.14m, 17));
        }
    }

    private readonly Clock _clock = new();
    private readonly CountingMaps _inner = new();
    private readonly CachedMapService _maps;

    private static readonly GeoPoint Ben = new(10.776889m, 106.700981m);
    private static readonly GeoPoint Airport = new(10.818463m, 106.658825m);

    public CachedMapServiceTests() =>
        _maps = new CachedMapService(_inner, new MemoryCache(new MemoryCacheOptions { Clock = _clock }));

    private Task<GeoPoint> Geocode(string address) => _maps.GeocodeAsync(address, CancellationToken.None);

    private Task<RouteInfo> Route(GeoPoint from, GeoPoint to) => _maps.GetRouteAsync(from, to, CancellationToken.None);

    [Fact]
    public async Task Same_address_is_geocoded_once()
    {
        var first = await Geocode("1 Lê Lợi, Q1");
        var second = await Geocode("1 Lê Lợi, Q1");

        Assert.Equal(1, _inner.GeocodeCalls);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Address_differing_only_in_case_and_spaces_hits_the_cache()
    {
        await Geocode("  1 Lê Lợi,   Q1 ");
        await Geocode("1 lê lợi, q1");

        Assert.Equal(1, _inner.GeocodeCalls);
    }

    [Fact]
    public async Task Address_without_diacritics_is_a_different_entry()
    {
        await Geocode("1 Lê Lợi, Q1");
        await Geocode("1 Le Loi, Q1");

        Assert.Equal(2, _inner.GeocodeCalls);
    }

    [Fact]
    public async Task Failed_geocode_is_not_cached()
    {
        _inner.Failures.Enqueue(new DomainException("Address not found.", "ADDRESS_NOT_FOUND"));

        var error = await Assert.ThrowsAsync<DomainException>(() => Geocode("1 Lê Lợi, Q1"));
        var point = await Geocode("1 Lê Lợi, Q1");

        Assert.Equal("ADDRESS_NOT_FOUND", error.Code);
        Assert.Equal(2, _inner.GeocodeCalls);
        Assert.Equal(new GeoPoint("1 Lê Lợi, Q1".Length, 2), point);
    }

    [Fact]
    public async Task Map_outage_is_not_cached()
    {
        _inner.Failures.Enqueue(new InvalidOperationException("MapVina search failed with status REQUEST_DENIED."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Geocode("1 Lê Lợi, Q1"));
        await Geocode("1 Lê Lợi, Q1");

        Assert.Equal(2, _inner.GeocodeCalls);
    }

    [Fact]
    public async Task Cancelled_geocode_is_not_cached()
    {
        _inner.Failures.Enqueue(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Geocode("1 Lê Lợi, Q1"));
        await Geocode("1 Lê Lợi, Q1");

        Assert.Equal(2, _inner.GeocodeCalls);
    }

    [Fact]
    public async Task Geocode_expires_after_seven_days()
    {
        await Geocode("1 Lê Lợi, Q1");

        _clock.UtcNow += CachedMapService.GeocodeTtl - TimeSpan.FromSeconds(1);
        await Geocode("1 Lê Lợi, Q1");
        Assert.Equal(1, _inner.GeocodeCalls);

        _clock.UtcNow += TimeSpan.FromSeconds(2);
        await Geocode("1 Lê Lợi, Q1");
        Assert.Equal(2, _inner.GeocodeCalls);
    }

    [Fact]
    public async Task Same_route_is_fetched_once()
    {
        var first = await Route(Ben, Airport);
        var second = await Route(Ben, Airport);

        Assert.Equal(1, _inner.RouteCalls);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Route_points_equal_after_rounding_hit_the_cache()
    {
        await Route(new GeoPoint(10.776889m, 106.700981m), Airport);
        await Route(new GeoPoint(10.7768891m, 106.7009809m), Airport);

        Assert.Equal(1, _inner.RouteCalls);
    }

    [Fact]
    public async Task Different_route_is_fetched_again()
    {
        await Route(Ben, Airport);
        await Route(Airport, Ben);

        Assert.Equal(2, _inner.RouteCalls);
    }

    [Fact]
    public async Task Route_expires_after_one_day()
    {
        await Route(Ben, Airport);

        _clock.UtcNow += CachedMapService.RouteTtl - TimeSpan.FromSeconds(1);
        await Route(Ben, Airport);
        Assert.Equal(1, _inner.RouteCalls);

        _clock.UtcNow += TimeSpan.FromSeconds(2);
        await Route(Ben, Airport);
        Assert.Equal(2, _inner.RouteCalls);
    }
}
