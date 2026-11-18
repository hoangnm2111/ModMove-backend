using System.Data;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Drivers.Commands.SetOnlineStatus;
using ModMove.Application.Features.Drivers.Queries.GetDriverStatus;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>UC-DRV-08, GB-03: the driver goes Online or Offline.</summary>
public class SetOnlineStatusTests
{
    private sealed class Setup
    {
        public User Driver { get; } = User.Create("Tai", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        public TestApp App { get; }
        public DriverProfile? Profile { get; private set; }

        public Setup(bool hasProfile = true, bool approved = true, AccountStatus status = AccountStatus.Active)
        {
            App = TestApp.SignedInAs(Driver);
            Driver.ChangeStatus(status);
            if (!hasProfile)
                return;

            Profile = DriverProfile.Create(Driver.Id, "front.jpg", "back.jpg");
            if (approved)
                Profile.Approve(Vehicle.Create(1, 1, "51C-1", 1000, "250x160x160", "r", "i", "p"));
            App.DriverProfiles.Add(Profile);
        }
    }

    [Fact]
    public async Task Approved_driver_goes_online_and_it_is_saved_GB03()
    {
        var s = new Setup();

        await s.App.Sender.Send(new SetOnlineStatusCommand(true));

        Assert.Equal(OnlineStatus.Online, s.Profile!.OnlineStatus);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Driver_goes_back_offline()
    {
        var s = new Setup();
        s.Profile!.GoOnline();

        await s.App.Sender.Send(new SetOnlineStatusCommand(false));

        Assert.Equal(OnlineStatus.Offline, s.Profile.OnlineStatus);
    }

    [Fact]
    public async Task Going_online_twice_is_harmless()
    {
        var s = new Setup();

        await s.App.Sender.Send(new SetOnlineStatusCommand(true));
        await s.App.Sender.Send(new SetOnlineStatusCommand(true));

        Assert.Equal(OnlineStatus.Online, s.Profile!.OnlineStatus);
    }

    [Fact]
    public async Task Profile_waiting_for_review_cannot_go_online_GB03()
    {
        var s = new Setup(approved: false);

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new SetOnlineStatusCommand(true)));

        Assert.Equal(OnlineStatus.Offline, s.Profile!.OnlineStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Driver_who_never_applied_cannot_go_online_GB03()
    {
        var s = new Setup(hasProfile: false);

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new SetOnlineStatusCommand(true)));

        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Theory]
    [InlineData(AccountStatus.Locked)]
    [InlineData(AccountStatus.Inactive)]
    public async Task Locked_or_deleted_account_is_refused_B8(AccountStatus status)
    {
        var s = new Setup(status: status);

        var forbidden = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new SetOnlineStatusCommand(true)));

        Assert.Equal("ACCOUNT_INACTIVE", forbidden.Code);
        Assert.Equal(OnlineStatus.Offline, s.Profile!.OnlineStatus);
    }

    private static ShippingOrder OrderOf(Setup s, OrderStatus status)
    {
        var order = ShippingOrder.Create(
            "MM20261020001", 77, vehicleTypeId: 1, couponId: null,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            12m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: 0,
            PaymentMethod.Cash, PayerRole.Sender, CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), TestApp.Start);
        order.Accept(s.Profile!.Id, s.Driver.Id, TestApp.Start);
        if (status >= OrderStatus.Arrived) order.ConfirmArrival(s.Driver.Id, TestApp.Start);
        if (status >= OrderStatus.PickedUp) order.ConfirmPickup(["1.jpg", "2.jpg"], s.Driver.Id, TestApp.Start);
        if (status >= OrderStatus.InTransit) order.ApprovePickupEvidence(77, TestApp.Start);
        if (status >= OrderStatus.Delivered) order.Deliver("pod.jpg", s.Driver.Id, TestApp.Start);
        s.App.Orders.Add(order);
        return order;
    }

    [Theory]
    [InlineData(OrderStatus.Accepted)]
    [InlineData(OrderStatus.Arrived)]
    [InlineData(OrderStatus.PickedUp)]
    [InlineData(OrderStatus.InTransit)]
    public async Task A_driver_cannot_go_offline_mid_trip(OrderStatus status)
    {
        var s = new Setup();
        s.Profile!.GoOnline();
        OrderOf(s, status);

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new SetOnlineStatusCommand(false)));

        Assert.Equal("DRIVER_HAS_ACTIVE_ORDER", ex.Code);
        Assert.Equal(OnlineStatus.Online, s.Profile.OnlineStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task A_driver_can_go_offline_once_the_order_is_delivered()
    {
        var s = new Setup();
        s.Profile!.GoOnline();
        OrderOf(s, OrderStatus.Delivered);

        await s.App.Sender.Send(new SetOnlineStatusCommand(false));

        Assert.Equal(OnlineStatus.Offline, s.Profile.OnlineStatus);
    }

    [Fact]
    public async Task Approved_driver_updates_location_via_command_and_refreshes_gps()
    {
        var s = new Setup();
        s.Profile!.GoOnline();

        await s.App.Sender.Send(new ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation.UpdateDriverLocationCommand(s.Driver.Id, 10.8012m, 106.7130m));

        Assert.Equal(10.8012m, s.Profile.LastLatitude);
        Assert.Equal(106.7130m, s.Profile.LastLongitude);
        Assert.True(s.Profile.HasFreshLocation(s.App.Time.Now));
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }
}

/// <summary>UC-DRV-08, DECISIONS 6.4 / GB-39: the driver home screen state and the 5-minute GPS rule.</summary>
public class GetDriverStatusTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private static TestApp App(AccountStatus status = AccountStatus.Active)
    {
        var driver = User.Create("Tai", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        var app = TestApp.SignedInAs(driver);
        driver.ChangeStatus(status);
        return app;
    }

    private static DataTable Row(
        string account = "Active", long? profileId = 7, string? approval = "Approved", string? online = "Online",
        DateTime? lastLocationAt = null, long? activeOrderId = null)
    {
        var t = new DataTable();
        t.Columns.Add("AccountStatus", typeof(string));
        t.Columns.Add("DriverProfileId", typeof(long));
        t.Columns.Add("ApprovalStatus", typeof(string));
        t.Columns.Add("OnlineStatus", typeof(string));
        t.Columns.Add("LastLatitude", typeof(decimal));
        t.Columns.Add("LastLongitude", typeof(decimal));
        t.Columns.Add("LastLocationAt", typeof(DateTime));
        t.Columns.Add("VehicleTypeId", typeof(long));
        t.Columns.Add("LicensePlate", typeof(string));
        t.Columns.Add("DepositBalance", typeof(decimal));
        t.Columns.Add("EarningsBalance", typeof(decimal));
        t.Columns.Add("WalletStatus", typeof(string));
        t.Columns.Add("ActiveOrderId", typeof(long));

        var hasProfile = profileId is not null;
        t.Rows.Add(
            account,
            (object?)profileId ?? DBNull.Value,
            (object?)approval ?? DBNull.Value,
            (object?)online ?? DBNull.Value,
            hasProfile && lastLocationAt is not null ? 21.03m : DBNull.Value,
            hasProfile && lastLocationAt is not null ? 105.85m : DBNull.Value,
            (object?)lastLocationAt ?? DBNull.Value,
            hasProfile ? 2L : DBNull.Value,
            hasProfile ? "51C-1" : DBNull.Value,
            hasProfile ? 300_000m : DBNull.Value,
            hasProfile ? 50_000m : DBNull.Value,
            hasProfile ? "Active" : DBNull.Value,
            (object?)activeOrderId ?? DBNull.Value);
        return t;
    }

    private static DateTime Ago(TimeSpan span) => (Now - span).UtcDateTime;

    [Fact]
    public async Task Online_approved_driver_with_fresh_gps_and_no_order_can_receive_orders()
    {
        var app = App();
        app.Sql.Result = Row(lastLocationAt: Ago(TimeSpan.FromSeconds(10)));

        var status = await app.Sender.Send(new GetDriverStatusQuery());

        Assert.True(status.HasProfile);
        Assert.True(status.IsOnline);
        Assert.False(status.IsGpsStale);
        Assert.True(status.CanReceiveOrders);
        Assert.Equal(("Approved", 2L, "51C-1"), (status.ApprovalStatus, status.VehicleTypeId, status.LicensePlate));
        Assert.Equal((300_000m, 50_000m, 21.03m, 105.85m), (status.DepositBalance, status.EarningsBalance, status.LastLatitude, status.LastLongitude));
        Assert.Equal(Now.AddSeconds(-10), status.LastLocationAt);
    }

    [Fact]
    public async Task Without_gps_for_5_minutes_the_driver_is_disconnected_6_4()
    {
        var app = App();
        app.Sql.Result = Row(lastLocationAt: Ago(TimeSpan.FromMinutes(5)));

        var status = await app.Sender.Send(new GetDriverStatusQuery());

        Assert.True(status.IsGpsStale);
        Assert.False(status.CanReceiveOrders);
        Assert.True(status.IsOnline); // still Online: only the matching and the map hide the driver
    }

    [Fact]
    public async Task Just_under_5_minutes_is_still_connected_6_4()
    {
        var app = App();
        app.Sql.Result = Row(lastLocationAt: Ago(TimeSpan.FromMinutes(5).Subtract(TimeSpan.FromSeconds(1))));

        var status = await app.Sender.Send(new GetDriverStatusQuery());

        Assert.False(status.IsGpsStale);
        Assert.True(status.CanReceiveOrders);
    }

    [Fact]
    public async Task Driver_who_never_sent_gps_is_stale()
    {
        var app = App();
        app.Sql.Result = Row(lastLocationAt: null);

        var status = await app.Sender.Send(new GetDriverStatusQuery());

        Assert.True(status.IsGpsStale);
        Assert.Null(status.LastLocationAt);
        Assert.False(status.CanReceiveOrders);
    }

    [Fact]
    public async Task Driver_with_an_order_in_progress_cannot_receive_another_GB09()
    {
        var app = App();
        app.Sql.Result = Row(lastLocationAt: Ago(TimeSpan.FromSeconds(5)), activeOrderId: 42);

        var status = await app.Sender.Send(new GetDriverStatusQuery());

        Assert.Equal(42, status.ActiveOrderId);
        Assert.False(status.CanReceiveOrders);
    }

    [Fact]
    public async Task Offline_driver_cannot_receive_orders()
    {
        var app = App();
        app.Sql.Result = Row(online: "Offline", lastLocationAt: Ago(TimeSpan.FromSeconds(5)));

        var status = await app.Sender.Send(new GetDriverStatusQuery());

        Assert.False(status.IsOnline);
        Assert.False(status.CanReceiveOrders);
    }

    [Fact]
    public async Task Driver_waiting_for_review_cannot_receive_orders_GB03()
    {
        var app = App();
        app.Sql.Result = Row(approval: "Pending", online: "Offline", lastLocationAt: null);

        var status = await app.Sender.Send(new GetDriverStatusQuery());

        Assert.Equal("Pending", status.ApprovalStatus);
        Assert.False(status.CanReceiveOrders);
    }

    [Fact]
    public async Task Driver_who_never_applied_gets_an_empty_state()
    {
        var app = App();
        app.Sql.Result = Row(profileId: null, approval: null, online: null);

        var status = await app.Sender.Send(new GetDriverStatusQuery());

        Assert.False(status.HasProfile);
        Assert.Null(status.ApprovalStatus);
        Assert.False(status.CanReceiveOrders);
        Assert.Equal((0m, 0m), (status.DepositBalance, status.EarningsBalance));
    }

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var app = App();
        app.Sql.Result = Row(account: "Locked");

        var forbidden = await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.Sender.Send(new GetDriverStatusQuery()));

        Assert.Equal("ACCOUNT_INACTIVE", forbidden.Code);
    }

    [Fact]
    public async Task Query_is_bound_to_the_caller_from_the_token_never_to_a_client_id()
    {
        var app = App();
        app.Sql.Result = Row(lastLocationAt: Ago(TimeSpan.FromSeconds(5)));

        await app.Sender.Send(new GetDriverStatusQuery());

        Assert.Equal(1L, app.Sql.LastParameters["UserId"]);
    }

    [Fact]
    public async Task Missing_user_row_means_sign_in_again()
    {
        var app = App();
        app.Sql.Result = new DataTable();

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new GetDriverStatusQuery()));
    }
}

