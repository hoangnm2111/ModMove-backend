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
using ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;
using ModMove.Application.Features.Tracking.Queries.CanFollowOrder;
using ModMove.Application.Features.Tracking.Queries.IsActiveAccount;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;
using ModMove.WebApi.Hubs;

namespace ModMove.IntegrationTests;

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
