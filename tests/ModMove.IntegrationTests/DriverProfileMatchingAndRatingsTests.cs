using System.Data;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Drivers.Queries.GetDriverProfile;
using ModMove.Application.Features.Matching.Queries.GetNearbyOrderDetail;
using ModMove.Application.Features.Ratings.Queries.GetDriverRatings;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// UC-DRV-06 (Driver Profile &amp; Vehicle), UC-DRV-09 (Job Request Details) and DECISIONS 7.5 / 8.3 (Driver Ratings).
/// </summary>
public class DriverProfileMatchingAndRatingsTests
{
    [Fact]
    public async Task GetDriverProfile_returns_personal_info_vehicle_dimensions_bank_and_rating_summary_DRV06()
    {
        var driver = User.Create("Nguyễn Văn Tài", "0912000001", "driver@x.vn", "h:x", UserRole.Driver, emailVerified: true);
        var app = TestApp.SignedInAs(driver);

        var dt = new DataTable();
        dt.Columns.Add("UserId", typeof(long));
        dt.Columns.Add("FullName", typeof(string));
        dt.Columns.Add("PhoneNumber", typeof(string));
        dt.Columns.Add("Email", typeof(string));
        dt.Columns.Add("EmailVerified", typeof(bool));
        dt.Columns.Add("AccountStatus", typeof(string));
        dt.Columns.Add("CreatedAt", typeof(DateTime));
        dt.Columns.Add("DriverProfileId", typeof(long));
        dt.Columns.Add("ApprovalStatus", typeof(string));
        dt.Columns.Add("OnlineStatus", typeof(string));
        dt.Columns.Add("LicenseFrontUrl", typeof(string));
        dt.Columns.Add("LicenseBackUrl", typeof(string));
        dt.Columns.Add("BankName", typeof(string));
        dt.Columns.Add("BankAccountNumber", typeof(string));
        dt.Columns.Add("BankAccountHolder", typeof(string));
        dt.Columns.Add("VehicleId", typeof(long));
        dt.Columns.Add("VehicleTypeId", typeof(long));
        dt.Columns.Add("VehicleTypeName", typeof(string));
        dt.Columns.Add("LicensePlate", typeof(string));
        dt.Columns.Add("MaxLoadWeightKg", typeof(decimal));
        dt.Columns.Add("CargoBedLengthCm", typeof(int));
        dt.Columns.Add("CargoBedWidthCm", typeof(int));
        dt.Columns.Add("CargoBedHeightCm", typeof(int));
        dt.Columns.Add("CargoBedDimensions", typeof(string));
        dt.Columns.Add("RegistrationCertUrl", typeof(string));
        dt.Columns.Add("InspectionCertUrl", typeof(string));
        dt.Columns.Add("VehiclePhotoUrl", typeof(string));
        dt.Columns.Add("VehicleStatus", typeof(string));
        dt.Columns.Add("TotalRatings", typeof(int));
        dt.Columns.Add("AverageStars", typeof(decimal));
        dt.Columns.Add("CompletedTrips", typeof(int));
        dt.Columns.Add("DocumentUrlsJson", typeof(string));

        dt.Rows.Add(
            driver.Id, "Nguyễn Văn Tài", "0912000001", "driver@x.vn", true, "Active", TestApp.Start.UtcDateTime,
            10L, "Approved", "Online", "https://cdn/f.jpg", "https://cdn/b.jpg",
            "Vietcombank", "0071001234567", "NGUYEN VAN TAI",
            20L, 1L, "Xe tải 1 tấn", "51C-123.45", 1000m, 300, 160, 170, "300x160x170",
            "https://cdn/reg.jpg", "https://cdn/ins.jpg", "https://cdn/veh.jpg", "Active",
            12, 4.8333m, 45,
            """["https://cdn/f.jpg","https://cdn/b.jpg","https://cdn/reg.jpg","https://cdn/ins.jpg","https://cdn/veh.jpg","https://cdn/cccd-f.jpg","https://cdn/cccd-b.jpg"]""");
        app.Sql.Results.Enqueue(dt);

        var profile = await app.Sender.Send(new GetDriverProfileQuery());

        Assert.True(profile.HasProfile);
        Assert.Equal("Approved", profile.ApprovalStatus);
        Assert.Equal("Vietcombank", profile.BankAccount!.BankName);
        Assert.Equal(("51C-123.45", 300, 160, 170), (profile.Vehicle!.LicensePlate, profile.Vehicle.CargoBedLengthCm, profile.Vehicle.CargoBedWidthCm, profile.Vehicle.CargoBedHeightCm));
        Assert.Equal((4.83m, 12, 45), (profile.AverageStars, profile.TotalRatings, profile.CompletedTrips));
        Assert.Equal(("https://cdn/cccd-f.jpg", "https://cdn/cccd-b.jpg"), (profile.CitizenIdFrontUrl, profile.CitizenIdBackUrl)); // D2
    }

    [Fact]
    public async Task GetNearbyOrderDetail_evaluates_eligibility_and_rejects_taken_cancelled_or_expired_orders_DRV09()
    {
        var driver = User.Create("Nguyễn Văn Tài", "0912000001", "driver@x.vn", "h:x", UserRole.Driver, emailVerified: true);
        var app = TestApp.SignedInAs(driver);
        app.Settings.Add(PlatformSetting.Create(SettingKeys.MatchingRadiusKm, "5", TestApp.Start));
        app.Settings.Add(PlatformSetting.Create(SettingKeys.RequiredDeposit, "200000", TestApp.Start));
        app.Settings.Add(PlatformSetting.Create(SettingKeys.CommissionRate, "0.10", TestApp.Start));

        // 1. Eligible candidate order -> CanAccept = true, DistanceToPickupKm & MatchingExpiresAt calculated
        app.Sql.Results.Enqueue(MatchingDriverTable(depositBalance: 250_000m));
        app.Sql.Results.Enqueue(CandidateOrderTable(orderStatus: "Pending", driverProfileId: null, createdAt: TestApp.Start.UtcDateTime));

        var detail = await app.Sender.Send(new GetNearbyOrderDetailQuery(101));
        Assert.True(detail.CanAccept);
        Assert.Null(detail.IneligibleReason);
        Assert.NotNull(detail.DistanceToPickupKm);
        Assert.Equal((200_000m, 20_000m, 180_000m), (detail.TotalFee, detail.CommissionAmount, detail.DriverEarning));
        Assert.Equal(TestApp.Start.AddMinutes(10), detail.MatchingExpiresAt);

        // 2. Insufficient COD deposit -> still viewable on Job Details screen, but CanAccept = false ("INSUFFICIENT_DEPOSIT")
        app.Sql.Results.Enqueue(MatchingDriverTable(depositBalance: 50_000m));
        app.Sql.Results.Enqueue(CandidateOrderTable(orderStatus: "Pending", driverProfileId: null, createdAt: TestApp.Start.UtcDateTime));

        var lowDeposit = await app.Sender.Send(new GetNearbyOrderDetailQuery(101));
        Assert.False(lowDeposit.CanAccept);
        Assert.Equal("INSUFFICIENT_DEPOSIT", lowDeposit.IneligibleReason);

        // 3. Order already accepted by another driver -> 409 CONFLICT (MSG40)
        app.Sql.Results.Enqueue(MatchingDriverTable(depositBalance: 250_000m));
        app.Sql.Results.Enqueue(CandidateOrderTable(orderStatus: "Accepted", driverProfileId: 99L, createdAt: TestApp.Start.UtcDateTime));
        await Assert.ThrowsAsync<ConflictException>(() => app.Sender.Send(new GetNearbyOrderDetailQuery(101)));

        // 4. Cancelled order -> 422 ORDER_CANCELLED
        app.Sql.Results.Enqueue(MatchingDriverTable(depositBalance: 250_000m));
        app.Sql.Results.Enqueue(CandidateOrderTable(orderStatus: "Cancelled", driverProfileId: null, createdAt: TestApp.Start.UtcDateTime));
        var cancelled = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new GetNearbyOrderDetailQuery(101)));
        Assert.Equal("ORDER_CANCELLED", cancelled.Code);

        // 5. Expired 10-minute matching window -> 422 ORDER_NOT_AVAILABLE
        app.Sql.Results.Enqueue(MatchingDriverTable(depositBalance: 250_000m));
        app.Sql.Results.Enqueue(CandidateOrderTable(orderStatus: "Pending", driverProfileId: null, createdAt: TestApp.Start.AddMinutes(-11).UtcDateTime));
        var expired = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new GetNearbyOrderDetailQuery(101)));
        Assert.Equal("ORDER_NOT_AVAILABLE", expired.Code);
    }

    [Fact]
    public async Task GetDriverRatings_returns_average_star_distribution_and_paginated_reviews_7_5()
    {
        var driver = User.Create("Nguyễn Văn Tài", "0912000001", "driver@x.vn", "h:x", UserRole.Driver, emailVerified: true);
        var app = TestApp.SignedInAs(driver);

        var ctx = new DataTable();
        ctx.Columns.Add("AccountStatus", typeof(string));
        ctx.Columns.Add("DriverProfileId", typeof(long));
        ctx.Columns.Add("ApprovalStatus", typeof(string));
        ctx.Columns.Add("BankName", typeof(string));
        ctx.Columns.Add("BankAccountNumber", typeof(string));
        ctx.Columns.Add("BankAccountHolder", typeof(string));
        ctx.Rows.Add("Active", 10L, "Approved", null, null, null);
        app.Sql.Results.Enqueue(ctx);

        var summary = new DataTable();
        summary.Columns.Add("TotalRatings", typeof(int));
        summary.Columns.Add("AverageStars", typeof(decimal));
        summary.Columns.Add("FiveStarCount", typeof(int));
        summary.Columns.Add("FourStarCount", typeof(int));
        summary.Columns.Add("ThreeStarCount", typeof(int));
        summary.Columns.Add("TwoStarCount", typeof(int));
        summary.Columns.Add("OneStarCount", typeof(int));
        summary.Rows.Add(3, 4.6667m, 2, 1, 0, 0, 0);
        app.Sql.Results.Enqueue(summary);

        var items = new DataTable();
        items.Columns.Add("Id", typeof(long));
        items.Columns.Add("OrderId", typeof(long));
        items.Columns.Add("OrderCode", typeof(string));
        items.Columns.Add("RatingStars", typeof(int));
        items.Columns.Add("ReviewComment", typeof(string));
        items.Columns.Add("CustomerName", typeof(string));
        items.Columns.Add("CreatedAt", typeof(DateTime));
        items.Rows.Add(1L, 55L, "MM20261108055", 5, "Tài xế rất nhiệt tình!", "Lê Thị Khách", TestApp.Start.UtcDateTime);
        app.Sql.Results.Enqueue(items);

        var response = await app.Sender.Send(new GetDriverRatingsQuery(Page: 1, PageSize: 20));

        Assert.Equal((4.67m, 3, 2, 1), (response.AverageStars, response.TotalRatings, response.FiveStarCount, response.FourStarCount));
        var review = Assert.Single(response.Reviews.Items);
        Assert.Equal((5, "MM20261108055"), (review.RatingStars, review.OrderCode));
    }

    private static DataTable MatchingDriverTable(decimal depositBalance)
    {
        var dt = new DataTable();
        dt.Columns.Add("AccountStatus", typeof(string));
        dt.Columns.Add("DriverProfileId", typeof(long));
        dt.Columns.Add("ApprovalStatus", typeof(string));
        dt.Columns.Add("OnlineStatus", typeof(string));
        dt.Columns.Add("LastLatitude", typeof(decimal));
        dt.Columns.Add("LastLongitude", typeof(decimal));
        dt.Columns.Add("LastLocationAt", typeof(DateTime));
        dt.Columns.Add("VehicleTypeId", typeof(long));
        dt.Columns.Add("VehicleStatus", typeof(string));
        dt.Columns.Add("WalletStatus", typeof(string));
        dt.Columns.Add("DepositBalance", typeof(decimal));
        dt.Columns.Add("ActiveOrderId", typeof(long));
        dt.Rows.Add(
            "Active", 10L, "Approved", "Online",
            10.7700m, 106.7000m, TestApp.Start.AddMinutes(-1).UtcDateTime,
            1L, "Active", "Active", depositBalance, DBNull.Value);
        return dt;
    }

    private static DataTable CandidateOrderTable(string orderStatus, long? driverProfileId, DateTime createdAt)
    {
        var dt = new DataTable();
        dt.Columns.Add("OrderId", typeof(long));
        dt.Columns.Add("OrderCode", typeof(string));
        dt.Columns.Add("OrderStatus", typeof(string));
        dt.Columns.Add("DriverProfileId", typeof(long));
        dt.Columns.Add("VehicleTypeId", typeof(long));
        dt.Columns.Add("VehicleTypeName", typeof(string));
        dt.Columns.Add("PickupAddress", typeof(string));
        dt.Columns.Add("PickupLatitude", typeof(decimal));
        dt.Columns.Add("PickupLongitude", typeof(decimal));
        dt.Columns.Add("DropoffAddress", typeof(string));
        dt.Columns.Add("DropoffLatitude", typeof(decimal));
        dt.Columns.Add("DropoffLongitude", typeof(decimal));
        dt.Columns.Add("EstimatedDistanceKm", typeof(decimal));
        dt.Columns.Add("EstimatedDurationMin", typeof(int));
        dt.Columns.Add("DistanceFee", typeof(decimal));
        dt.Columns.Add("ServiceFee", typeof(decimal));
        dt.Columns.Add("DiscountAmount", typeof(decimal));
        dt.Columns.Add("TotalFee", typeof(decimal));
        dt.Columns.Add("PaymentMethod", typeof(string));
        dt.Columns.Add("PaymentStatus", typeof(string));
        dt.Columns.Add("PayerRole", typeof(string));
        dt.Columns.Add("HasLoadingService", typeof(bool));
        dt.Columns.Add("CreatedAt", typeof(DateTime));
        dt.Columns.Add("PaidAt", typeof(DateTime));
        dt.Columns.Add("CargoType", typeof(string));
        dt.Columns.Add("CargoTypeName", typeof(string));
        dt.Columns.Add("CargoQuantity", typeof(int));
        dt.Columns.Add("CargoWeightKg", typeof(decimal));
        dt.Columns.Add("CargoLengthCm", typeof(int));
        dt.Columns.Add("CargoWidthCm", typeof(int));
        dt.Columns.Add("CargoHeightCm", typeof(int));
        dt.Columns.Add("CargoPhotoUrlsJson", typeof(string));
        dt.Columns.Add("SpecialNotes", typeof(string));

        dt.Rows.Add(
            101L, "MM20261108101", orderStatus, driverProfileId.HasValue ? driverProfileId.Value : DBNull.Value,
            1L, "Xe tải 1 tấn",
            "12 Nguyễn Huệ, Q1", 10.7750m, 106.7030m,
            "45 Lê Văn Việt, Thủ Đức", 10.8400m, 106.7800m,
            12.5m, 35, 180_000m, 40_000m, 20_000m, 200_000m,
            "Cash", "Unpaid", "Receiver", true, createdAt, DBNull.Value,
            "FURNITURE", "Đồ nội thất", 2, 80m, 120, 60, 60,
            "[\"https://res.cloudinary.com/demo/cargo.jpg\"]", "Gọi trước 5 phút");
        return dt;
    }
}

