using System.Data;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Matching.Queries.GetNearbyOrders;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>
/// GET api/driver/matching/orders (UC-DRV-09, DECISIONS B5, B10, 3.2, 6.4, GB-08, GB-09, GB-10, GB-14, GB-16, GB-24, GB-31)
/// through the real MediatR pipeline with <see cref="FakeSql"/>.
/// </summary>
public class GetNearbyOrdersTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private static TestApp CreateApp(
        decimal matchingRadiusKm = 5m,
        decimal requiredDeposit = 200_000m,
        decimal commissionRate = 0.10m)
    {
        var driver = User.Create("Tai", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        var app = TestApp.SignedInAs(driver);
        app.Settings.Add(PlatformSetting.Create(SettingKeys.MatchingRadiusKm, matchingRadiusKm.ToString(System.Globalization.CultureInfo.InvariantCulture), Now));
        app.Settings.Add(PlatformSetting.Create(SettingKeys.RequiredDeposit, requiredDeposit.ToString(System.Globalization.CultureInfo.InvariantCulture), Now));
        app.Settings.Add(PlatformSetting.Create(SettingKeys.CommissionRate, commissionRate.ToString(System.Globalization.CultureInfo.InvariantCulture), Now));
        return app;
    }

    private static DataTable DriverRow(
        string accountStatus = "Active",
        long? profileId = 7L,
        string? approvalStatus = "Approved",
        string? onlineStatus = "Online",
        decimal? lastLat = 21.0285m,
        decimal? lastLon = 105.8542m,
        DateTime? lastLocationAt = null,
        bool useDefaultFreshGps = true,
        long? vehicleTypeId = 1L,
        string? vehicleStatus = "Active",
        string? walletStatus = "Active",
        decimal depositBalance = 300_000m,
        long? activeOrderId = null)
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
        t.Columns.Add("VehicleStatus", typeof(string));
        t.Columns.Add("WalletStatus", typeof(string));
        t.Columns.Add("DepositBalance", typeof(decimal));
        t.Columns.Add("ActiveOrderId", typeof(long));

        var effectiveGpsAt = lastLocationAt ?? (useDefaultFreshGps ? Now.AddSeconds(-15).UtcDateTime : null);
        var hasProfile = profileId is not null;

        t.Rows.Add(
            accountStatus,
            (object?)profileId ?? DBNull.Value,
            hasProfile ? (object?)approvalStatus ?? DBNull.Value : DBNull.Value,
            hasProfile ? (object?)onlineStatus ?? DBNull.Value : DBNull.Value,
            hasProfile && effectiveGpsAt is not null && lastLat is not null ? lastLat.Value : DBNull.Value,
            hasProfile && effectiveGpsAt is not null && lastLon is not null ? lastLon.Value : DBNull.Value,
            hasProfile && effectiveGpsAt is not null ? effectiveGpsAt.Value : DBNull.Value,
            hasProfile && vehicleTypeId is not null ? vehicleTypeId.Value : DBNull.Value,
            hasProfile ? (object?)vehicleStatus ?? DBNull.Value : DBNull.Value,
            hasProfile ? (object?)walletStatus ?? DBNull.Value : DBNull.Value,
            hasProfile ? depositBalance : 0m,
            (object?)activeOrderId ?? DBNull.Value);
        return t;
    }

    private static DataTable OrdersTable()
    {
        var t = new DataTable();
        t.Columns.Add("OrderId", typeof(long));
        t.Columns.Add("OrderCode", typeof(string));
        t.Columns.Add("OrderStatus", typeof(string));
        t.Columns.Add("VehicleTypeId", typeof(long));
        t.Columns.Add("PickupAddress", typeof(string));
        t.Columns.Add("PickupLatitude", typeof(decimal));
        t.Columns.Add("PickupLongitude", typeof(decimal));
        t.Columns.Add("DropoffAddress", typeof(string));
        t.Columns.Add("DropoffLatitude", typeof(decimal));
        t.Columns.Add("DropoffLongitude", typeof(decimal));
        t.Columns.Add("EstimatedDistanceKm", typeof(decimal));
        t.Columns.Add("EstimatedDurationMin", typeof(int));
        t.Columns.Add("TotalFee", typeof(decimal));
        t.Columns.Add("PaymentMethod", typeof(string));
        t.Columns.Add("PaymentStatus", typeof(string));
        t.Columns.Add("PayerRole", typeof(string));
        t.Columns.Add("HasLoadingService", typeof(bool));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("PaidAt", typeof(DateTime));
        t.Columns.Add("CargoType", typeof(string));
        t.Columns.Add("CargoTypeName", typeof(string));
        t.Columns.Add("CargoQuantity", typeof(int));
        t.Columns.Add("CargoWeightKg", typeof(decimal));
        t.Columns.Add("CargoLengthCm", typeof(int));
        t.Columns.Add("CargoWidthCm", typeof(int));
        t.Columns.Add("CargoHeightCm", typeof(int));
        t.Columns.Add("CargoPhotoUrlsJson", typeof(string));
        t.Columns.Add("SpecialNotes", typeof(string));
        return t;
    }

    private static void AddCandidate(
        DataTable t,
        long orderId = 100L,
        string orderCode = "MM-20261020-0100",
        string orderStatus = "Pending",
        long vehicleTypeId = 1L,
        decimal pickupLat = 21.0300m,
        decimal pickupLon = 105.8550m,
        decimal totalFee = 500_000m,
        string paymentMethod = "Cash",
        string paymentStatus = "Unpaid",
        string? payerRole = "Sender",
        bool hasLoadingService = true,
        DateTime? createdAt = null,
        DateTime? paidAt = null,
        string photoUrlsJson = "[\"https://res.cloudinary.com/demo/image/upload/cargo/1.jpg\"]",
        string? specialNotes = "Hàng dễ vỡ")
    {
        t.Rows.Add(
            orderId,
            orderCode,
            orderStatus,
            vehicleTypeId,
            "1 Đinh Tiên Hoàng, Hoàn Kiếm",
            pickupLat,
            pickupLon,
            "100 Cầu Giấy, Hà Nội",
            21.0362m,
            105.7906m,
            8.5m,
            25,
            totalFee,
            paymentMethod,
            paymentStatus,
            (object?)payerRole ?? DBNull.Value,
            hasLoadingService,
            createdAt ?? Now.AddMinutes(-2).UtcDateTime,
            (object?)paidAt ?? DBNull.Value,
            "FURNITURE",
            "Đồ nội thất",
            2,
            120m,
            100,
            60,
            80,
            photoUrlsJson,
            (object?)specialNotes ?? DBNull.Value);
    }

    [Fact]
    public async Task GetNearbyOrders_signs_private_photos_DEF011()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow());
        var orders = OrdersTable();
        AddCandidate(orders, photoUrlsJson: FakeFileStorage.MixedPhotosJson);
        app.Sql.Results.Enqueue(orders);

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        Assert.Equal(FakeFileStorage.MixedPhotosViewed, Assert.Single(result).CargoPhotoUrls);
    }

    [Fact]
    public async Task Returns_eligible_orders_within_radius_sorted_nearest_first_B5_GB08_GB14()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow());

        var orders = OrdersTable();
        // Order 101: ~1.4 km away, Cash, TotalFee = 500k -> Commission = 50k, DriverEarning = 450k
        AddCandidate(orders, orderId: 101L, orderCode: "MM-20261020-0101", pickupLat: 21.0400m, pickupLon: 105.8600m, totalFee: 500_000m);
        // Order 102: ~0.19 km away (nearest), PayOS Paid, TotalFee = 800k -> Commission = 80k, DriverEarning = 720k
        AddCandidate(
            orders,
            orderId: 102L,
            orderCode: "MM-20261020-0102",
            pickupLat: 21.0300m,
            pickupLon: 105.8550m,
            totalFee: 800_000m,
            paymentMethod: "PayOS",
            paymentStatus: "Paid",
            payerRole: null,
            paidAt: Now.AddMinutes(-1).UtcDateTime,
            photoUrlsJson: "[\"https://img/a.jpg\",\"https://img/b.jpg\"]");
        app.Sql.Results.Enqueue(orders);

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        Assert.Equal(2, result.Count);
        Assert.Equal(102L, result[0].OrderId);
        Assert.Equal(101L, result[1].OrderId);
        Assert.True(result[0].DistanceToPickupKm < result[1].DistanceToPickupKm);

        Assert.Equal(80_000m, result[0].CommissionAmount);
        Assert.Equal(720_000m, result[0].DriverEarning);
        Assert.Equal(["https://img/a.jpg", "https://img/b.jpg"], result[0].CargoPhotoUrls);
        Assert.Equal("Đồ nội thất", result[0].CargoTypeName);

        Assert.Equal(50_000m, result[1].CommissionAmount);
        Assert.Equal(450_000m, result[1].DriverEarning);
    }

    [Fact]
    public async Task Hides_orders_outside_matching_radius_GB08_B5()
    {
        var app = CreateApp(matchingRadiusKm: 5m);
        app.Sql.Results.Enqueue(DriverRow());

        var orders = OrdersTable();
        // ~0.2 km away (inside 5 km)
        AddCandidate(orders, orderId: 201L, pickupLat: 21.0300m, pickupLon: 105.8550m);
        // ~6.5 km away (outside 5 km)
        AddCandidate(orders, orderId: 202L, pickupLat: 21.0850m, pickupLon: 105.8700m);
        app.Sql.Results.Enqueue(orders);

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        var single = Assert.Single(result);
        Assert.Equal(201L, single.OrderId);
        Assert.Contains("pickup_latitude BETWEEN @MinLat AND @MaxLat", app.Sql.LastSql);
        Assert.Contains("pickup_longitude BETWEEN @MinLon AND @MaxLon", app.Sql.LastSql);
        Assert.True((decimal)app.Sql.LastParameters["MinLat"]! < 21.0285m);
        Assert.True((decimal)app.Sql.LastParameters["MaxLat"]! > 21.0285m);
        Assert.True((decimal)app.Sql.LastParameters["MinLon"]! < 105.8542m);
        Assert.True((decimal)app.Sql.LastParameters["MaxLon"]! > 105.8542m);
    }

    [Fact]
    public async Task Hides_orders_of_different_vehicle_type_GB10()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow(vehicleTypeId: 1L));

        var orders = OrdersTable();
        AddCandidate(orders, orderId: 301L, vehicleTypeId: 1L);
        AddCandidate(orders, orderId: 302L, vehicleTypeId: 2L);
        app.Sql.Results.Enqueue(orders);

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        var single = Assert.Single(result);
        Assert.Equal(301L, single.OrderId);
    }

    [Fact]
    public async Task Hides_unpaid_payos_orders_and_shows_paid_ones_GB31_B10()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow());

        var orders = OrdersTable();
        // Unpaid PayOS -> hidden until webhook confirms payment
        AddCandidate(orders, orderId: 401L, paymentMethod: "PayOS", paymentStatus: "Unpaid", paidAt: null);
        // Paid PayOS -> visible
        AddCandidate(orders, orderId: 402L, paymentMethod: "PayOS", paymentStatus: "Paid", paidAt: Now.AddMinutes(-1).UtcDateTime);
        app.Sql.Results.Enqueue(orders);

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        var single = Assert.Single(result);
        Assert.Equal(402L, single.OrderId);
    }

    [Fact]
    public async Task Hides_orders_past_10_minute_matching_window_and_uses_paid_at_for_payos_GB24_3_2()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow());

        var orders = OrdersTable();
        // Cash order created exactly 10 minutes ago -> expired
        AddCandidate(orders, orderId: 501L, paymentMethod: "Cash", createdAt: Now.AddMinutes(-10).UtcDateTime);
        // Cash order created 9m59s ago -> still open
        AddCandidate(orders, orderId: 502L, paymentMethod: "Cash", createdAt: Now.AddMinutes(-10).AddSeconds(1).UtcDateTime);
        // PayOS order created 25 minutes ago, but paid 2 minutes ago -> still open (window starts at paid_at per 3.2)
        AddCandidate(
            orders,
            orderId: 503L,
            paymentMethod: "PayOS",
            paymentStatus: "Paid",
            createdAt: Now.AddMinutes(-25).UtcDateTime,
            paidAt: Now.AddMinutes(-2).UtcDateTime);
        // PayOS order paid 10 minutes ago -> expired
        AddCandidate(
            orders,
            orderId: 504L,
            paymentMethod: "PayOS",
            paymentStatus: "Paid",
            createdAt: Now.AddMinutes(-15).UtcDateTime,
            paidAt: Now.AddMinutes(-10).UtcDateTime);
        app.Sql.Results.Enqueue(orders);

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        Assert.Equal([502L, 503L], result.Select(o => o.OrderId).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task Hides_all_orders_when_deposit_below_required_deposit_200k_GB16()
    {
        var app = CreateApp(requiredDeposit: 200_000m);
        app.Sql.Results.Enqueue(DriverRow(depositBalance: 199_999m));

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        Assert.Empty(result);
        Assert.Contains("FROM users u", app.Sql.LastSql);
    }

    [Fact]
    public async Task Hides_cash_order_when_order_commission_exceeds_deposit_balance_GB16()
    {
        var app = CreateApp(requiredDeposit: 200_000m, commissionRate: 0.10m);
        // Deposit is 220,000 (>= 200k required deposit, but < 250k commission of order 701)
        app.Sql.Results.Enqueue(DriverRow(depositBalance: 220_000m));

        var orders = OrdersTable();
        // Commission = 250,000 > 220,000 -> hidden
        AddCandidate(orders, orderId: 701L, paymentMethod: "Cash", totalFee: 2_500_000m);
        // Commission = 180,000 <= 220,000 -> visible
        AddCandidate(orders, orderId: 702L, paymentMethod: "Cash", totalFee: 1_800_000m);
        app.Sql.Results.Enqueue(orders);

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        var single = Assert.Single(result);
        Assert.Equal(702L, single.OrderId);
    }

    [Fact]
    public async Task Hides_all_orders_when_wallet_is_not_active_GB16()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow(walletStatus: "Frozen", depositBalance: 500_000m));

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        Assert.Empty(result);
        Assert.Contains("FROM users u", app.Sql.LastSql);
    }

    [Fact]
    public async Task Offline_driver_gets_empty_list_without_querying_orders()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow(onlineStatus: "Offline"));

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        Assert.Empty(result);
        Assert.Empty(app.Sql.Results);
    }

    [Fact]
    public async Task Stale_gps_at_or_beyond_5_minutes_gets_empty_list_6_4()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow(lastLocationAt: Now.AddMinutes(-5).UtcDateTime));

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Driver_without_gps_gets_empty_list_6_4()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow(lastLocationAt: null, useDefaultFreshGps: false));

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Driver_with_active_order_gets_empty_list_GB09()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow(activeOrderId: 99L));

        var result = await app.Sender.Send(new GetNearbyOrdersQuery());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Unapproved_driver_or_inactive_vehicle_gets_empty_list_GB03()
    {
        var app1 = CreateApp();
        app1.Sql.Results.Enqueue(DriverRow(approvalStatus: "Pending"));
        Assert.Empty(await app1.Sender.Send(new GetNearbyOrdersQuery()));

        var app2 = CreateApp();
        app2.Sql.Results.Enqueue(DriverRow(vehicleStatus: "Inactive"));
        Assert.Empty(await app2.Sender.Send(new GetNearbyOrdersQuery()));
    }

    [Theory]
    [InlineData("Locked")]
    [InlineData("Inactive")]
    public async Task Locked_or_deleted_account_is_refused_B8(string accountStatus)
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(DriverRow(accountStatus: accountStatus));

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.Sender.Send(new GetNearbyOrdersQuery()));

        Assert.Equal("ACCOUNT_INACTIVE", ex.Code);
    }

    [Fact]
    public async Task Missing_user_row_throws_unauthorized()
    {
        var app = CreateApp();
        app.Sql.Results.Enqueue(new DataTable());

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new GetNearbyOrdersQuery()));
    }
}
