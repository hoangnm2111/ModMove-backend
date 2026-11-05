using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Matching.Commands.AcceptOrder;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// POST api/driver/matching/orders/{id}/accept (UC-DRV-10, GB-08..10, GB-16, GB-24, GB-31, DECISIONS B5, B10, B12, 3.2, 6.4, 8.3)
/// through the real MediatR pipeline.
/// </summary>
public class AcceptOrderTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private sealed class Setup
    {
        public User Customer { get; }
        public User Driver { get; }
        public DriverProfile Profile { get; }
        public DriverWallet Wallet { get; }
        public TestApp App { get; }

        public Setup(
            AccountStatus accountStatus = AccountStatus.Active,
            bool approved = true,
            bool online = true,
            long vehicleTypeId = 1L,
            decimal? gpsLat = 21.0285m,
            decimal? gpsLon = 105.8542m,
            DateTimeOffset? gpsAt = null,
            bool hasGps = true,
            decimal depositBalance = 300_000m)
        {
            Customer = User.Create("Khách", "0900000001", "khach@x.vn", "h", UserRole.Customer, emailVerified: true);
            Driver = User.Create("Tài", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
            Driver.ChangeStatus(accountStatus);

            App = TestApp.SignedInAs(Driver, Customer);
            App.Settings.Add(PlatformSetting.Create(SettingKeys.MatchingRadiusKm, "5", Now));
            App.Settings.Add(PlatformSetting.Create(SettingKeys.MinCodDeposit, "200000", Now));
            App.Settings.Add(PlatformSetting.Create(SettingKeys.CommissionRate, "0.10", Now));

            Profile = DriverProfile.Create(Driver.Id, "001", "B2-001", "front.jpg", "back.jpg");
            if (approved)
            {
                Profile.Approve(Vehicle.Create(1, vehicleTypeId, "51C-12345", 1000, "250x160x160", "r", "i", "p"));
                if (online)
                    Profile.GoOnline();
            }

            if (hasGps && gpsLat is not null && gpsLon is not null)
                Profile.UpdateLocation(gpsLat.Value, gpsLon.Value, gpsAt ?? Now.AddSeconds(-15));

            App.DriverProfiles.Add(Profile);

            Wallet = DriverWallet.Create(Profile.Id);
            if (depositBalance >= 50_000m)
                Wallet.TopUpDeposit(depositBalance);
            App.DriverWallets.Add(Wallet);
        }

        public ShippingOrder AddOrder(
            PaymentMethod method = PaymentMethod.Cash,
            bool paid = false,
            long vehicleTypeId = 1L,
            decimal pickupLat = 21.0300m,
            decimal pickupLon = 105.8550m,
            decimal distanceFee = 200_000m,
            DateTimeOffset? createdAt = null,
            DateTimeOffset? paidAt = null)
        {
            var order = ShippingOrder.Create(
                "MM20261020001",
                Customer.Id,
                vehicleTypeId,
                couponId: null,
                "1 Đinh Tiên Hoàng",
                pickupLat,
                pickupLon,
                "100 Cầu Giấy",
                21.0362m,
                105.7906m,
                "Bình",
                "0912345678",
                8.5m,
                25,
                hasLoadingService: false,
                distanceFee: distanceFee,
                serviceFee: 0m,
                discountAmount: 0m,
                method,
                method == PaymentMethod.Cash ? PayerRole.Sender : null,
                CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null),
                Now);
            order.CreatedAt = createdAt ?? Now.AddMinutes(-1);
            if (paid)
                order.MarkPaid(paidAt ?? Now.AddMinutes(-1));
            App.Orders.Add(order);
            return order;
        }
    }

    [Fact]
    public async Task Eligible_driver_accepts_cash_order_updates_state_and_notifies_customer_GB08_B12_8_3()
    {
        var s = new Setup();
        var order = s.AddOrder(PaymentMethod.Cash);

        await s.App.Sender.Send(new AcceptOrderCommand(order.Id));

        Assert.Equal(OrderStatus.Accepted, order.OrderStatus);
        Assert.Equal(s.Profile.Id, order.DriverProfileId);
        Assert.Equal(Now, order.AcceptedAt);
        Assert.Equal(1, order.RowVersion);
        Assert.Equal(1, s.App.UnitOfWork.Saves);

        // Customer receives ORDER_ACCEPTED notification (8.3)
        var notifEvent = Assert.Single(s.App.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal(("user", s.Customer.Id.ToString()), (notifEvent.Target, notifEvent.Id));
        var notif = Assert.IsType<Notification>(notifEvent.Payload);
        Assert.Equal("ORDER_ACCEPTED", notif.Type);
        Assert.Contains(order.OrderCode, notif.Body);
        Assert.Equal(order.Id.ToString(), notif.Data!["orderId"]);

        // OrderStatusChanged sent to both order-{id} group and driver user (B12)
        var statusTargets = s.App.Realtime.Sent
            .Where(x => x.EventName == RealtimeEvents.OrderStatusChanged)
            .Select(x => (x.Target, x.Id))
            .ToArray();
        Assert.Equal([("order", order.Id.ToString()), ("user", s.Driver.Id.ToString())], statusTargets);
    }

    [Fact]
    public async Task Eligible_driver_with_zero_deposit_can_still_accept_paid_payos_order_GB16_GB31()
    {
        var s = new Setup(depositBalance: 0m);
        var order = s.AddOrder(PaymentMethod.PayOS, paid: true);

        await s.App.Sender.Send(new AcceptOrderCommand(order.Id));

        Assert.Equal(OrderStatus.Accepted, order.OrderStatus);
        Assert.Equal(s.Profile.Id, order.DriverProfileId);
    }

    [Fact]
    public async Task Second_driver_trying_to_accept_already_accepted_order_gets_409_conflict_GB08()
    {
        var s = new Setup();
        var order = s.AddOrder(PaymentMethod.Cash);
        await s.App.Sender.Send(new AcceptOrderCommand(order.Id));

        // Second driver tries to accept the same order
        var driver2 = User.Create("Tài 2", "0900000004", "tai2@x.vn", "h", UserRole.Driver, emailVerified: true);
        s.App.Users.Add(driver2);
        var profile2 = DriverProfile.Create(driver2.Id, "002", "B2-002", "f.jpg", "b.jpg");
        profile2.Approve(Vehicle.Create(2, 1L, "51C-99999", 1000, "250x160x160", "r", "i", "p"));
        profile2.GoOnline();
        profile2.UpdateLocation(21.0290m, 105.8545m, Now);
        s.App.DriverProfiles.Add(profile2);

        await Assert.ThrowsAsync<ConflictException>(() => s.App.Sender.Send(new AcceptOrderCommand(order.Id)));
        Assert.Equal(s.Profile.Id, order.DriverProfileId);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Concurrent_save_race_on_row_version_throws_409_conflict_GB08()
    {
        var s = new Setup();
        var order = s.AddOrder(PaymentMethod.Cash);
        s.App.UnitOfWork.ThrowConflictOnSave = true;

        await Assert.ThrowsAsync<ConflictException>(() => s.App.Sender.Send(new AcceptOrderCommand(order.Id)));
        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public async Task Unpaid_payos_order_cannot_be_accepted_GB31_B10()
    {
        var s = new Setup();
        var order = s.AddOrder(PaymentMethod.PayOS, paid: false);

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new AcceptOrderCommand(order.Id)));
        Assert.Equal(OrderStatus.Pending, order.OrderStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Cancelled_order_cannot_be_accepted()
    {
        var s = new Setup();
        var order = s.AddOrder(PaymentMethod.Cash);
        order.CancelByCustomer(s.Customer.Id, "Đổi ý", Now);

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new AcceptOrderCommand(order.Id)));
        Assert.Equal("ORDER_CANCELLED", ex.Code);
    }

    [Fact]
    public async Task Order_past_10_minute_matching_window_cannot_be_accepted_GB24_3_2()
    {
        var s = new Setup();
        var expiredCash = s.AddOrder(PaymentMethod.Cash, createdAt: Now.AddMinutes(-10));

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new AcceptOrderCommand(expiredCash.Id)));
        Assert.Equal(OrderStatus.Pending, expiredCash.OrderStatus);
    }

    [Fact]
    public async Task Rejects_offline_unapproved_stale_gps_wrong_vehicle_outside_radius_busy_or_low_deposit_driver()
    {
        // 1. Offline driver
        var offline = new Setup(online: false);
        var o1 = offline.AddOrder();
        var ex1 = await Assert.ThrowsAsync<DomainException>(() => offline.App.Sender.Send(new AcceptOrderCommand(o1.Id)));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex1.Code);

        // 2. Unapproved driver
        var unapproved = new Setup(approved: false);
        var o2 = unapproved.AddOrder();
        var ex2 = await Assert.ThrowsAsync<DomainException>(() => unapproved.App.Sender.Send(new AcceptOrderCommand(o2.Id)));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex2.Code);

        // 3. Stale GPS (5 minutes old, DECISIONS 6.4)
        var staleGps = new Setup(gpsAt: Now.AddMinutes(-5));
        var o3 = staleGps.AddOrder();
        var ex3 = await Assert.ThrowsAsync<DomainException>(() => staleGps.App.Sender.Send(new AcceptOrderCommand(o3.Id)));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex3.Code);

        // 4. Wrong vehicle type (GB-10)
        var wrongVehicle = new Setup(vehicleTypeId: 1L);
        var o4 = wrongVehicle.AddOrder(vehicleTypeId: 2L);
        var ex4 = await Assert.ThrowsAsync<DomainException>(() => wrongVehicle.App.Sender.Send(new AcceptOrderCommand(o4.Id)));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex4.Code);

        // 5. Outside 5 km radius (GB-08)
        var farPickup = new Setup();
        var o5 = farPickup.AddOrder(pickupLat: 21.1500m, pickupLon: 105.9500m);
        var ex5 = await Assert.ThrowsAsync<DomainException>(() => farPickup.App.Sender.Send(new AcceptOrderCommand(o5.Id)));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex5.Code);

        // 6. Driver already has an active order (GB-09)
        var busy = new Setup();
        var existingOrder = busy.AddOrder();
        await busy.App.Sender.Send(new AcceptOrderCommand(existingOrder.Id));
        var secondOrder = busy.AddOrder();
        var ex6 = await Assert.ThrowsAsync<DomainException>(() => busy.App.Sender.Send(new AcceptOrderCommand(secondOrder.Id)));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex6.Code);

        // 7. Cash order where commission (250k) > deposit (220k) even though deposit >= 200k MIN_COD_DEPOSIT (GB-16)
        var lowDepositForBigOrder = new Setup(depositBalance: 220_000m);
        var bigCashOrder = lowDepositForBigOrder.AddOrder(PaymentMethod.Cash, distanceFee: 2_500_000m);
        var ex7 = await Assert.ThrowsAsync<DomainException>(() => lowDepositForBigOrder.App.Sender.Send(new AcceptOrderCommand(bigCashOrder.Id)));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex7.Code);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public async Task Invalid_order_id_is_rejected_by_validator(long invalidId)
    {
        var s = new Setup();
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new AcceptOrderCommand(invalidId)));
    }

    [Fact]
    public async Task Unknown_order_throws_not_found()
    {
        var s = new Setup();
        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new AcceptOrderCommand(9999L)));
    }

    [Theory]
    [InlineData(AccountStatus.Locked)]
    [InlineData(AccountStatus.Inactive)]
    public async Task Locked_or_deleted_account_is_refused_B8(AccountStatus status)
    {
        var s = new Setup(accountStatus: status);
        var order = s.AddOrder();

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new AcceptOrderCommand(order.Id)));
        Assert.Equal("ACCOUNT_INACTIVE", ex.Code);
    }
}

