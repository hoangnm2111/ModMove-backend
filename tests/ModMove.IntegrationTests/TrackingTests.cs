using System.Data;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Tracking.Commands.PurgeOldLocationLogs;
using ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;
using ModMove.Application.Features.Tracking.Queries.CanFollowOrder;
using ModMove.Application.Features.Tracking.Queries.IsActiveAccount;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;
using ModMove.WebApi.Hubs;

namespace ModMove.IntegrationTests;

/// <summary>JOB-GPS-01: GPS points are kept for 30 days.</summary>
public class PurgeOldLocationLogsTests
{
    [Fact]
    public async Task Points_older_than_30_days_are_deleted_and_newer_ones_kept()
    {
        var app = new TestApp();
        var now = TestApp.Start;
        app.LocationLogs.Add(DriverLocationLog.Record(1, 1, 21m, 105m, now.AddDays(-30).AddSeconds(-1)));
        var onTheLine = DriverLocationLog.Record(1, 1, 21m, 105m, now.AddDays(-30));
        app.LocationLogs.Add(onTheLine);

        var deleted = await app.Sender.Send(new PurgeOldLocationLogsCommand());

        Assert.Equal(1, deleted);
        Assert.Equal(onTheLine, Assert.Single(app.LocationLogs.Items));
    }
}

/// <summary>SendLocation behind /hubs/tracking (DECISIONS B12, 6.4).</summary>
public class UpdateDriverLocationTests
{
    private static TripSetup Driver(bool online = true)
    {
        var s = new TripSetup();
        s.Profile.Approve(Vehicle.Create(s.Profile.Id, 1, "51C-1", 500, "1x1x1", "r", "i", "p"));
        if (online)
            s.Profile.GoOnline();
        return s;
    }

    [Fact]
    public async Task Driver_with_an_order_updates_the_map_and_the_order_followers_B12()
    {
        var s = Driver();
        var order = s.PickedUp();

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m));

        Assert.Equal((21.03m, 105.85m, TripSetup.Now), (s.Profile.LastLatitude!.Value, s.Profile.LastLongitude!.Value, s.Profile.LastLocationAt!.Value));
        var log = Assert.Single(s.App.LocationLogs.Items);
        Assert.Equal((s.Profile.Id, (long?)order.Id, 21.03m), (log.DriverProfileId, log.OrderId, log.Latitude));
        Assert.Equal(1, s.App.UnitOfWork.Saves);
        Assert.Equal(
            [("order", order.Id.ToString(), RealtimeEvents.LocationUpdated), ("dispatchers", null, RealtimeEvents.LocationUpdated)],
            s.App.Realtime.Sent.Select(x => (x.Target, x.Id, x.EventName)));
        var payload = Assert.IsType<DriverLocationUpdate>(s.App.Realtime.Sent[0].Payload);
        Assert.Equal((s.Profile.Id, (long?)order.Id), (payload.DriverProfileId, payload.OrderId));
    }

    [Fact]
    public async Task Without_an_order_nothing_is_logged_but_dispatchers_still_see_the_driver_B12()
    {
        var s = Driver();

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m));

        Assert.Empty(s.App.LocationLogs.Items);
        Assert.Equal("dispatchers", Assert.Single(s.App.Realtime.Sent).Target);
    }

    [Fact]
    public async Task Delivered_order_is_no_longer_followed()
    {
        var s = Driver();
        s.Delivered();

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m));

        Assert.Empty(s.App.LocationLogs.Items);
    }

    [Fact]
    public async Task Offline_driver_is_kept_off_the_dispatcher_map()
    {
        var s = Driver(online: false);

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m));

        Assert.Equal(21.03m, s.Profile.LastLatitude);
        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public async Task Driver_not_approved_is_refused()
    {
        var s = new TripSetup();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21m, 105m)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new UpdateDriverLocationCommand(s.Customer.Id, 21m, 105m)));
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Locked_driver_is_refused_B8()
    {
        var s = Driver();
        s.Driver.ChangeStatus(AccountStatus.Locked);

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21m, 105m)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Theory]
    [InlineData(91, 105)]
    [InlineData(21, -181)]
    public async Task Coordinates_must_be_real(int lat, int lng) =>
        await Assert.ThrowsAsync<ValidationException>(() => Driver().App.Sender.Send(new UpdateDriverLocationCommand(3, lat, lng)));

    [Fact]
    public async Task Accuracy_below_zero_is_invalid()
    {
        var s = Driver();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21m, 105m, Accuracy: -1m)));
    }

    [Fact]
    public async Task Inaccurate_point_is_dropped_but_accepted_B19()
    {
        var s = Driver();
        s.PickedUp();

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m, Accuracy: 150m));

        Assert.Null(s.Profile.LastLatitude);
        Assert.Empty(s.App.LocationLogs.Items);
        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public async Task Mock_location_is_refused_with_MOCK_LOCATION_B20()
    {
        var s = Driver();
        s.PickedUp();

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m, IsMocked: true)));

        Assert.Equal("MOCK_LOCATION", error.Code);
        Assert.Null(s.Profile.LastLatitude);
        Assert.Empty(s.App.LocationLogs.Items);
        Assert.Empty(s.App.Realtime.Sent);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(-601)]
    public async Task RecordedAt_far_in_the_future_or_older_than_10_minutes_is_invalid_B21(int secondsFromNow)
    {
        var s = Driver();

        var error = await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(
            new UpdateDriverLocationCommand(s.Driver.Id, 21m, 105m, RecordedAt: s.App.Time.Now.AddSeconds(secondsFromNow))));

        Assert.Contains("RecordedAt", error.Errors.Keys);
        Assert.Null(s.Profile.LastLatitude);
    }

    [Fact]
    public async Task Past_recordedAt_inside_the_window_is_kept_as_the_point_time_B21()
    {
        var s = Driver();
        var at = s.App.Time.Now.AddMinutes(-10);

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m, RecordedAt: at));

        Assert.Equal(at, s.Profile.LastLocationAt);
        Assert.Equal(at, Assert.IsType<DriverLocationUpdate>(Assert.Single(s.App.Realtime.Sent).Payload).At);
    }

    [Fact]
    public async Task Slightly_future_recordedAt_counts_as_server_time_so_live_points_keep_moving_the_driver_B21()
    {
        var s = Driver();
        var now = s.App.Time.Now;

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m, RecordedAt: now.AddSeconds(30)));
        Assert.Equal(now, s.Profile.LastLocationAt);

        s.App.Time.Now = now.AddSeconds(5);
        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.04m, 105.86m));

        Assert.Equal((21.04m, now.AddSeconds(5)), (s.Profile.LastLatitude!.Value, s.Profile.LastLocationAt!.Value));
        Assert.Equal(2, s.App.Realtime.Sent.Count);
    }

    [Fact]
    public async Task Mock_location_wins_over_an_out_of_range_recordedAt_B20()
    {
        var s = Driver();

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(
            new UpdateDriverLocationCommand(s.Driver.Id, 21m, 105m, IsMocked: true, RecordedAt: s.App.Time.Now.AddHours(1))));

        Assert.Equal("MOCK_LOCATION", error.Code);
    }

    [Fact]
    public async Task Backlog_point_older_than_the_last_one_is_logged_but_does_not_move_the_driver_B21()
    {
        var s = Driver();
        var order = s.PickedUp();
        s.Profile.UpdateLocation(21.10m, 105.90m, s.App.Time.Now);

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m, RecordedAt: s.App.Time.Now.AddMinutes(-2)));

        Assert.Equal((21.10m, 105.90m), (s.Profile.LastLatitude!.Value, s.Profile.LastLongitude!.Value));
        var log = Assert.Single(s.App.LocationLogs.Items);
        Assert.Equal(((long?)order.Id, 21.03m, s.App.Time.Now.AddMinutes(-2)), (log.OrderId, log.Latitude, log.RecordedAt));
        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public async Task Points_30_seconds_apart_are_logged_once_but_both_move_the_driver_B12()
    {
        var s = Driver();
        s.PickedUp();

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m));
        s.App.Time.Now = s.App.Time.Now.AddSeconds(30);
        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.04m, 105.86m));

        Assert.Equal(21.03m, Assert.Single(s.App.LocationLogs.Items).Latitude);
        Assert.Equal((21.04m, s.App.Time.Now), (s.Profile.LastLatitude!.Value, s.Profile.LastLocationAt!.Value));
        Assert.Equal(4, s.App.Realtime.Sent.Count); // order followers and dispatchers, twice
    }

    [Fact]
    public async Task Points_60_seconds_apart_are_both_logged_B12()
    {
        var s = Driver();
        s.PickedUp();

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m));
        s.App.Time.Now = s.App.Time.Now.AddSeconds(60);
        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.04m, 105.86m));

        Assert.Equal([21.03m, 21.04m], s.App.LocationLogs.Items.Select(l => l.Latitude));
    }

    [Fact]
    public async Task A_recent_point_of_another_order_does_not_hold_back_the_log_B12()
    {
        var s = Driver();
        var order = s.PickedUp();
        s.App.LocationLogs.Add(DriverLocationLog.Record(s.Profile.Id, order.Id + 100, 21m, 105m, s.App.Time.Now.AddSeconds(-10)));

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m));

        Assert.Single(s.App.LocationLogs.Items, l => l.OrderId == order.Id);
    }

    [Fact]
    public async Task Backlog_point_older_than_the_last_log_is_not_logged_B12_B21()
    {
        var s = Driver();
        var order = s.PickedUp();
        s.App.LocationLogs.Add(DriverLocationLog.Record(s.Profile.Id, order.Id, 21m, 105m, s.App.Time.Now.AddMinutes(-1)));

        await s.App.Sender.Send(new UpdateDriverLocationCommand(s.Driver.Id, 21.03m, 105.85m, RecordedAt: s.App.Time.Now.AddMinutes(-3)));

        Assert.Single(s.App.LocationLogs.Items);
    }
}

/// <summary>Who may connect and follow an order on /hubs/tracking (B12, B8, C10); rows served by FakeSql.</summary>
public class TrackingAccessQueryTests
{
    private sealed class Db
    {
        public FakeSql Sql { get; } = new();
        public string? AccountStatus { get; set; } = "Active";
        public bool OrderIsTheirs { get; set; } = true;
        public List<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> Executed { get; } = [];

        public Db()
        {
            Sql.CustomQuery = (text, parameters) =>
            {
                Executed.Add((text, parameters));
                if (text.Contains("FROM users"))
                    return AccountStatus is null ? Empty("AccountStatus") : Table(("AccountStatus", typeof(string), AccountStatus));
                return OrderIsTheirs ? Table(("Id", typeof(long), 7L)) : Empty("Id");
            };
        }

        public ISender Sender() => new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(Sql)
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

    private static DataTable Empty(string column)
    {
        var table = new DataTable();
        table.Columns.Add(column, typeof(string));
        return table;
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Locked", false)]
    [InlineData("Inactive", false)]
    [InlineData(null, false)]
    public async Task Only_an_active_account_may_stay_connected_B12(string? status, bool expected) =>
        Assert.Equal(expected, await new Db { AccountStatus = status }.Sender().Send(new IsActiveAccountQuery(5)));

    [Fact]
    public async Task Customer_follows_only_their_own_order_C10()
    {
        var db = new Db();

        Assert.True(await db.Sender().Send(new CanFollowOrderQuery(5, 7)));

        var (sql, parameters) = db.Executed.Last(e => e.Sql.Contains("FROM shipping_orders"));
        Assert.Contains("customer_id = @UserId", sql);
        Assert.Equal((5L, 7L), ((long)parameters["UserId"]!, (long)parameters["OrderId"]!));
        Assert.False(await new Db { OrderIsTheirs = false }.Sender().Send(new CanFollowOrderQuery(5, 7)));
    }

    [Fact]
    public async Task Locked_customer_follows_nothing_B8() =>
        Assert.False(await new Db { AccountStatus = "Locked" }.Sender().Send(new CanFollowOrderQuery(5, 7)));
}

/// <summary>TrackingHub stays thin: it checks access through queries and maps errors to a HubException code.</summary>
public class TrackingHubTests
{
    private sealed class FakeContext(long userId, UserRole role) : HubCallerContext
    {
        public bool Aborted { get; private set; }
        public override string ConnectionId => "conn-1";
        public override string? UserIdentifier => userId.ToString();
        public override ClaimsPrincipal? User { get; } = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role.ToString())], "jwt"));
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() => Aborted = true;
    }

    private sealed class FakeGroups : IGroupManager
    {
        public List<string> Joined { get; } = [];
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Joined.Add(groupName);
            return Task.CompletedTask;
        }
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSender(Func<object, object?> handle) : ISender
    {
        public List<object> Sent { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult((TResponse)handle(request)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            Sent.Add(request);
            handle(request);
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult(handle(request));
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static (TrackingHub Hub, FakeContext Context, FakeGroups Groups, FakeSender Sender) Build(
        UserRole role, Func<object, object?> handle)
    {
        var sender = new FakeSender(handle);
        var context = new FakeContext(5, role);
        var groups = new FakeGroups();
        return (new TrackingHub(sender) { Context = context, Groups = groups }, context, groups, sender);
    }

    [Fact]
    public async Task Inactive_account_is_disconnected_B12()
    {
        var (hub, context, groups, _) = Build(UserRole.Dispatcher, _ => false);

        await hub.OnConnectedAsync();

        Assert.True(context.Aborted);
        Assert.Empty(groups.Joined);
    }

    [Fact]
    public async Task Dispatcher_joins_the_dispatchers_group_on_connect_B12()
    {
        var (hub, context, groups, _) = Build(UserRole.Dispatcher, _ => true);

        await hub.OnConnectedAsync();

        Assert.False(context.Aborted);
        Assert.Equal([TrackingHub.DispatchersGroup], groups.Joined);
    }

    [Fact]
    public async Task Customer_joins_no_group_on_connect()
    {
        var (hub, _, groups, _) = Build(UserRole.Customer, _ => true);

        await hub.OnConnectedAsync();

        Assert.Empty(groups.Joined);
    }

    [Fact]
    public async Task Customer_follows_their_own_order()
    {
        var (hub, _, groups, sender) = Build(UserRole.Customer, _ => true);

        await hub.JoinOrder(7);

        Assert.Equal(["order-7"], groups.Joined);
        Assert.Equal(new CanFollowOrderQuery(5, 7), Assert.Single(sender.Sent));
    }

    [Fact]
    public async Task Someone_elses_order_is_refused_with_ORDER_NOT_YOURS_C10()
    {
        var (hub, _, groups, _) = Build(UserRole.Customer, _ => false);

        var error = await Assert.ThrowsAsync<HubException>(() => hub.JoinOrder(7));

        Assert.StartsWith("ORDER_NOT_YOURS", error.Message);
        Assert.Empty(groups.Joined);
    }

    [Fact]
    public async Task SendLocation_uses_the_caller_from_the_token()
    {
        var (hub, _, _, sender) = Build(UserRole.Driver, _ => null);

        await hub.SendLocation(21.03m, 105.85m);

        Assert.Equal(new UpdateDriverLocationCommand(5, 21.03m, 105.85m), Assert.Single(sender.Sent));
    }

    [Theory]
    [InlineData(nameof(TrackingHub.JoinOrder), nameof(UserRole.Customer))]
    [InlineData(nameof(TrackingHub.SendLocation), nameof(UserRole.Driver))]
    public void Each_hub_method_is_for_one_role_B12(string method, string role)
    {
        var attribute = typeof(TrackingHub).GetMethod(method)!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single();

        Assert.Equal(role, attribute.Roles);
        Assert.NotEmpty(typeof(TrackingHub).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: false));
    }

    [Fact]
    public async Task Errors_reach_the_client_as_a_stable_code()
    {
        var (hub, _, _, _) = Build(UserRole.Driver, _ => throw new ForbiddenAccessException("The account is not active.", "ACCOUNT_INACTIVE"));
        var (hub2, _, _, _) = Build(UserRole.Driver, _ => throw new DomainException("Bad.", "SOME_RULE"));

        Assert.StartsWith("ACCOUNT_INACTIVE", (await Assert.ThrowsAsync<HubException>(() => hub.SendLocation(1, 1))).Message);
        Assert.StartsWith("SOME_RULE", (await Assert.ThrowsAsync<HubException>(() => hub2.SendLocation(1, 1))).Message);
    }
}
