using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Tracking.Queries.GetOrderTracking;

namespace ModMove.IntegrationTests;

/// <summary>GET api/customer/orders/{id}/tracking (UC-CUS-13; DECISIONS B12, 6.4); rows served by FakeSql.</summary>
public class OrderTrackingTests
{
    private const long CustomerId = 7;
    private static readonly DateTime SeenAt = new(2026, 10, 20, 7, 58, 0); // 2 minutes before TestApp.Start

    private sealed class RecordingMaps : IMapService
    {
        public List<(GeoPoint From, GeoPoint To)> Routes { get; } = [];
        public bool Fail { get; set; }
        public bool Hang { get; set; }

        public Task<GeoPoint> GeocodeAsync(string address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public async Task<RouteInfo> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Hang)
                await Task.Delay(Timeout.Infinite, cancellationToken);
            if (Fail)
                throw new HttpRequestException("Google Maps is down");
            Routes.Add((from, to));
            return new RouteInfo(5m, 14);
        }
    }

    private sealed class Db
    {
        public FakeSql Sql { get; } = new();
        public RecordingMaps Maps { get; } = new();
        public string AccountStatus { get; set; } = "Active";
        public string OrderStatus { get; set; } = "InTransit";
        public DateTime? LocationAt { get; set; } = SeenAt;
        public bool Found { get; set; } = true;

        /// <summary>Where the incident behind a mid-trip reassignment happened; null = no such reassignment.</summary>
        public (decimal? Latitude, decimal? Longitude)? Handover { get; set; }

        public List<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> Executed { get; } = [];

        public Db()
        {
            Sql.CustomQuery = (text, parameters) =>
            {
                Executed.Add((text, parameters));
                if (text.Contains("FROM users"))
                    return Table(("AccountStatus", typeof(string), AccountStatus));
                if (text.Contains("FROM in_transit_incidents"))
                {
                    var handover = Table(("Latitude", typeof(decimal), Handover?.Latitude), ("Longitude", typeof(decimal), Handover?.Longitude));
                    if (Handover is null)
                        handover.Rows.Clear();
                    return handover;
                }

                var row = Table(
                    ("OrderStatus", typeof(string), OrderStatus),
                    ("PickupLatitude", typeof(decimal), 21.0m), ("PickupLongitude", typeof(decimal), 105.8m),
                    ("DropoffLatitude", typeof(decimal), 21.1m), ("DropoffLongitude", typeof(decimal), 105.9m),
                    ("DriverLatitude", typeof(decimal), LocationAt is null ? null : 21.05m),
                    ("DriverLongitude", typeof(decimal), LocationAt is null ? null : 105.85m),
                    ("DriverLocationAt", typeof(DateTime), LocationAt));
                if (!Found)
                    row.Rows.Clear();
                return row;
            };
        }

        public ISender Sender() => new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(Sql)
            .AddSingleton<IMapService>(Maps)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(CustomerId))
            .AddSingleton<TimeProvider>(new MutableTime(TestApp.Start))
            .BuildServiceProvider()
            .GetRequiredService<ISender>();
    }

    private static DataTable Table(params (string Name, Type Type, object? Value)[] columns)
    {
        var table = new DataTable();
        foreach (var c in columns)
            table.Columns.Add(c.Name, c.Type);
        table.Rows.Add(columns.Select(c => c.Value ?? DBNull.Value).ToArray());
        return table;
    }

    [Theory]
    [InlineData("Accepted", "Pickup", 21.0, 105.8)]
    [InlineData("Arrived", "Pickup", 21.0, 105.8)]
    [InlineData("PickedUp", "Dropoff", 21.1, 105.9)]
    [InlineData("InTransit", "Dropoff", 21.1, 105.9)]
    public async Task Eta_runs_from_the_driver_to_the_next_stop(string status, string target, double latitude, double longitude)
    {
        var db = new Db { OrderStatus = status };

        var tracking = await db.Sender().Send(new GetOrderTrackingQuery(1));

        Assert.Equal((target, 14), (tracking.Eta!.Target, tracking.Eta.DurationMinutes));
        var (from, to) = Assert.Single(db.Maps.Routes);
        Assert.Equal(new GeoPoint(21.05m, 105.85m), from);
        Assert.Equal(new GeoPoint((decimal)latitude, (decimal)longitude), to);
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("Arrived")]
    public async Task After_a_mid_trip_reassignment_the_eta_runs_to_the_old_vehicle_7_2(string status)
    {
        var db = new Db { OrderStatus = status, Handover = (21.07m, 105.87m) };

        var tracking = await db.Sender().Send(new GetOrderTrackingQuery(1));

        Assert.Equal(("Handover", 14), (tracking.Eta!.Target, tracking.Eta.DurationMinutes));
        Assert.Equal(new GeoPoint(21.07m, 105.87m), Assert.Single(db.Maps.Routes).To);
    }

    [Fact]
    public async Task Handover_without_a_known_location_has_no_eta_7_2()
    {
        var db = new Db { OrderStatus = "Accepted", Handover = (null, null) };

        var tracking = await db.Sender().Send(new GetOrderTrackingQuery(1));

        Assert.NotNull(tracking.Driver);
        Assert.Null(tracking.Eta);
        Assert.Empty(db.Maps.Routes);
    }

    [Fact]
    public async Task Only_a_reassignment_after_pickup_on_this_order_counts_as_a_handover()
    {
        var db = new Db { OrderStatus = "Accepted" };

        await db.Sender().Send(new GetOrderTrackingQuery(1));

        var (sql, parameters) = db.Executed.Last(e => e.Sql.Contains("FROM in_transit_incidents"));
        Assert.Contains("JOIN proof_of_deliveries", sql); // created when the cargo is picked up
        Assert.Contains("resolution_decision = 'ReassignDriver'", sql);
        Assert.Contains("ORDER BY i.resolved_at DESC", sql);
        Assert.Equal(1L, parameters["OrderId"]);
    }

    [Theory]
    [InlineData("PickedUp")]
    [InlineData("InTransit")]
    public async Task Once_the_new_driver_has_the_cargo_the_eta_runs_to_the_dropoff_7_2(string status)
    {
        var db = new Db { OrderStatus = status, Handover = (21.07m, 105.87m) };

        var tracking = await db.Sender().Send(new GetOrderTrackingQuery(1));

        Assert.Equal("Dropoff", tracking.Eta!.Target);
    }

    [Fact]
    public async Task Snapshot_has_the_stops_and_the_drivers_last_position()
    {
        var tracking = await new Db().Sender().Send(new GetOrderTrackingQuery(1));

        Assert.Equal((1L, "InTransit"), (tracking.OrderId, tracking.OrderStatus));
        Assert.Equal(new GeoPoint(21.0m, 105.8m), tracking.Pickup);
        Assert.Equal(new GeoPoint(21.1m, 105.9m), tracking.Dropoff);
        Assert.Equal((21.05m, 105.85m, false), (tracking.Driver!.Latitude, tracking.Driver.Longitude, tracking.Driver.IsStale));
        Assert.Equal(new DateTimeOffset(SeenAt, TimeSpan.Zero), tracking.Driver.At);
    }

    [Theory]
    [InlineData("PlaceOrder")]
    [InlineData("Delivered")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task Driver_position_is_shown_only_during_the_trip(string status)
    {
        var db = new Db { OrderStatus = status };

        var tracking = await db.Sender().Send(new GetOrderTrackingQuery(1));

        Assert.Null(tracking.Driver);
        Assert.Null(tracking.Eta);
        Assert.Empty(db.Maps.Routes);
    }

    [Fact]
    public async Task Position_older_than_5_minutes_is_stale_and_has_no_eta_6_4()
    {
        var db = new Db { LocationAt = new DateTime(2026, 10, 20, 7, 54, 59) };

        var tracking = await db.Sender().Send(new GetOrderTrackingQuery(1));

        Assert.True(tracking.Driver!.IsStale);
        Assert.Null(tracking.Eta);
        Assert.Empty(db.Maps.Routes);
    }

    [Fact]
    public async Task Position_exactly_5_minutes_old_still_counts_6_4()
    {
        var db = new Db { LocationAt = new DateTime(2026, 10, 20, 7, 55, 0) };

        var tracking = await db.Sender().Send(new GetOrderTrackingQuery(1));

        Assert.False(tracking.Driver!.IsStale);
        Assert.NotNull(tracking.Eta);
    }

    [Fact]
    public async Task Driver_who_never_sent_gps_has_no_position()
    {
        var db = new Db { LocationAt = null };

        var tracking = await db.Sender().Send(new GetOrderTrackingQuery(1));

        Assert.Null(tracking.Driver);
        Assert.Null(tracking.Eta);
    }

    [Fact]
    public async Task Maps_failure_still_returns_the_snapshot()
    {
        var db = new Db();
        db.Maps.Fail = true;

        var tracking = await db.Sender().Send(new GetOrderTrackingQuery(1));

        Assert.NotNull(tracking.Driver);
        Assert.Null(tracking.Eta);
    }

    [Fact]
    public async Task Maps_that_hangs_gives_up_within_seconds()
    {
        var db = new Db();
        db.Maps.Hang = true;

        var tracking = await db.Sender().Send(new GetOrderTrackingQuery(1)).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.NotNull(tracking.Driver);
        Assert.Null(tracking.Eta);
    }

    [Fact]
    public async Task Cancelled_request_is_not_swallowed_as_a_maps_failure()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new Db().Sender().Send(new GetOrderTrackingQuery(1), cancelled.Token));
    }

    [Fact]
    public async Task Only_the_callers_order_is_read_C10()
    {
        var db = new Db();

        await db.Sender().Send(new GetOrderTrackingQuery(1));

        var (sql, parameters) = db.Executed.Last(e => e.Sql.Contains("FROM shipping_orders"));
        Assert.Contains("o.customer_id = @CustomerId", sql);
        Assert.Equal((CustomerId, 1L), (parameters["CustomerId"], parameters["OrderId"]));
    }

    [Fact]
    public async Task Someone_elses_order_is_404() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new Db { Found = false }.Sender().Send(new GetOrderTrackingQuery(1)));

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new Db { AccountStatus = "Locked" }.Sender().Send(new GetOrderTrackingQuery(1)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Fact]
    public async Task Signed_out_caller_is_unauthorized()
    {
        var sender = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(new FakeSql())
            .AddSingleton<IMapService>(new RecordingMaps())
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(null))
            .AddSingleton<TimeProvider>(new MutableTime(TestApp.Start))
            .BuildServiceProvider()
            .GetRequiredService<ISender>();

        await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new GetOrderTrackingQuery(1)));
    }

    [Fact]
    public async Task Order_id_must_be_positive() =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetOrderTrackingQuery(0)));
}
