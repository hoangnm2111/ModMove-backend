using System.Data;
using MediatR;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Catalog.Queries.GetVehicleTypes;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;
using ModMove.Application.Features.Incidents.Queries.GetMyIncidents;
using ModMove.Application.Features.MasterData.Queries.GetPublicContent;
using ModMove.Application.Features.Orders.Commands.ConfirmReceipt;
using ModMove.Application.Features.Orders.Commands.CreateOrder;
using ModMove.Application.Features.Orders.Commands.ReviewPickupEvidence;
using ModMove.Application.Features.Orders.Commands.UploadImage;
using ModMove.Application.Features.Orders.Queries.GetAvailableCoupons;
using ModMove.Application.Features.Orders.Queries.GetMyOrderDetail;
using ModMove.Application.Features.Orders.Queries.GetMyOrders;
using ModMove.Application.Features.Ratings.Commands.RateTrip;
using ModMove.Application.Features.Ratings.Queries.GetMyRatings;
using ModMove.Application.Features.Refunds.Commands.RequestIncidentRefund;
using ModMove.Application.Features.Refunds.Commands.SubmitComplaint;
using ModMove.Application.Features.Refunds.Commands.WithdrawRefund;
using ModMove.Application.Features.Refunds.Queries.GetMyRefunds;
using ModMove.Application.Features.Tracking.Queries.GetOrderTracking;
using ModMove.Application.Features.Users.Commands.DeleteMyAccount;
using ModMove.Application.Features.Users.Commands.UpdateFcmToken;
using ModMove.Application.Features.Users.Commands.UpdateMyProfile;
using ModMove.Application.Features.Users.Queries.GetMyProfile;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// Gaps found while mapping the SRS use cases of the Guest and Customer to the existing tests: the public read queries,
/// the order business errors the estimate shares with booking, and the signed-out check of every customer use case.
/// </summary>
public class CustomerCoverageGapTests
{
    private const string Photo = "https://res.cloudinary.com/demo/image/upload/modmove/customers/1/a.jpg";

    private static CreateOrderCommand Order(string pickup = "Hoàn Kiếm, Hà Nội", decimal weight = 100, string? coupon = null) =>
        new(1, pickup, "Cầu Giấy, Hà Nội", "Bình", "0912345678",
            new CargoInput("FURNITURE", 2, weight, 100, 100, 100, [Photo], null),
            false, coupon, PaymentMethod.Cash, PayerRole.Sender, true);

    private static TestApp CustomerApp()
    {
        var customer = User.Create("An", "0900000001", "an@x.vn", "h:secret123", UserRole.Customer, emailVerified: true);
        var app = TestApp.SignedInAs(customer);
        app.VehicleTypes.Add(VehicleType.Create("Xe van 500kg", 500, 170, 120, 120, 50_000, 10_000, 80_000));
        app.Coupons.Add(Coupon.Create("WELCOME20K", 20_000, 100_000, TestApp.Start.AddDays(-1), TestApp.Start.AddYears(1), 1_000));
        app.Maps.Route = new RouteInfo(12m, 30);
        return app;
    }

    // ---------------------------------------------------------- Public reads

    [Fact]
    public async Task Vehicle_types_are_read_from_active_rows_only_UC_GST_02()
    {
        var app = new TestApp();
        var rows = new DataTable();
        foreach (var column in new[] { "Id", "Name", "MaxLoadWeightKg", "CargoBedLengthCm", "CargoBedWidthCm", "CargoBedHeightCm", "BaseFare", "PricePerKm", "MinimumFare" })
            rows.Columns.Add(column, column == "Name" ? typeof(string) : typeof(decimal));
        rows.Rows.Add(1m, "Xe van 500kg", 500m, 170m, 120m, 120m, 50_000m, 10_000m, 80_000m);
        rows.Rows.Add(2m, "Xe tải 1 tấn", 1_000m, 300m, 160m, 160m, 80_000m, 14_000m, 150_000m);
        app.Sql.Result = rows;

        var result = await app.Sender.Send(new GetVehicleTypesQuery());

        Assert.Equal(["Xe van 500kg", "Xe tải 1 tấn"], result.Select(v => v.Name));
        Assert.Equal(500m, result[0].MaxLoadWeightKg);
        Assert.Contains("is_active = 1", app.Sql.LastSql);
        Assert.Contains("ORDER BY max_load_weight_kg", app.Sql.LastSql);
    }

    [Theory]
    [InlineData(PublicContentCategories.Policy)]
    [InlineData(PublicContentCategories.ServiceInfo)]
    [InlineData(PublicContentCategories.CargoType)]
    [InlineData(PublicContentCategories.ComplaintReason)]
    [InlineData(PublicContentCategories.CancellationReason)]
    [InlineData(PublicContentCategories.Bank)]
    public async Task Public_content_reads_one_category_as_a_parameter_UC_GST_03_04(string category)
    {
        var app = new TestApp();
        var rows = new DataTable();
        rows.Columns.Add("Code", typeof(string));
        rows.Columns.Add("Name", typeof(string));
        rows.Columns.Add("Description", typeof(string));
        rows.Rows.Add("OTHER", "Khác", DBNull.Value);
        app.Sql.Result = rows;

        var result = await app.Sender.Send(new GetPublicContentQuery(category));

        var item = Assert.Single(result);
        Assert.Equal(("OTHER", "Khác", null), (item.Code, item.Name, item.Description));
        Assert.Equal(category, app.Sql.LastParameters["Category"]);
        Assert.Contains("is_active = 1", app.Sql.LastSql);
        Assert.DoesNotContain(category, app.Sql.LastSql); // the category is never written into the SQL text
    }

    [Fact]
    public async Task Public_content_with_no_rows_is_an_empty_list()
    {
        var app = new TestApp();
        app.Sql.Result = new DataTable();

        var result = await app.Sender.Send(new GetPublicContentQuery(PublicContentCategories.Policy));

        Assert.Empty(result);
    }

    // ------------------------------------------------- Order business errors

    [Fact]
    public async Task Unknown_pickup_address_stops_the_order_and_saves_nothing_UC_GST_07()
    {
        var app = CustomerApp();

        var error = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(Order(pickup: FakeMapService.UnknownAddress, coupon: "WELCOME20K")));

        Assert.Equal("ADDRESS_NOT_FOUND", error.Code);
        Assert.Empty(app.Orders.Items);
        Assert.Equal(0, app.Coupons.Items[0].UsedCount);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Cargo_over_the_vehicle_load_stops_the_order_GB_10()
    {
        var app = CustomerApp();

        var error = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(Order(weight: 501)));

        Assert.Equal("VEHICLE_TOO_SMALL", error.Code);
        Assert.Empty(app.Orders.Items);
    }

    [Fact]
    public async Task Unknown_coupon_stops_the_order_and_counts_no_use_GST_14()
    {
        var app = CustomerApp();

        var error = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(Order(coupon: "NOPE")));

        Assert.Equal("COUPON_NOT_FOUND", error.Code);
        Assert.Empty(app.Orders.Items);
        Assert.Equal(0, app.Coupons.Items[0].UsedCount);
    }

    [Fact]
    public async Task Unknown_vehicle_type_stops_the_order()
    {
        var app = CustomerApp();

        await Assert.ThrowsAsync<NotFoundException>(() => app.Sender.Send(Order() with { VehicleTypeId = 99 }));

        Assert.Empty(app.Orders.Items);
    }

    // -------------------------------------------- Signed-out caller, GB-28/50

    public static TheoryData<string, object> CustomerRequests => new()
    {
        { nameof(CreateOrderCommand), Order() },
        { nameof(GetMyOrdersQuery), new GetMyOrdersQuery() },
        { nameof(GetMyOrderDetailQuery), new GetMyOrderDetailQuery(1) },
        { nameof(GetAvailableCouponsQuery), new GetAvailableCouponsQuery() },
        { nameof(UploadImageCommand), new UploadImageCommand(new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]), "a.jpg", "image/jpeg", 4) },
        { nameof(ReviewPickupEvidenceCommand), new ReviewPickupEvidenceCommand(1, true, null) },
        { nameof(ConfirmReceiptCommand), new ConfirmReceiptCommand(1) },
        { nameof(RateTripCommand), new RateTripCommand(1, 5, null) },
        { nameof(GetMyRatingsQuery), new GetMyRatingsQuery() },
        { nameof(ReportIncidentByCustomerCommand), new ReportIncidentByCustomerCommand(1, IncidentType.Other, "Late", null, null, null) },
        { nameof(GetMyIncidentsQuery), new GetMyIncidentsQuery() },
        { nameof(SubmitComplaintCommand), new SubmitComplaintCommand(1, "DAMAGED_CARGO", null, null, 100_000, TestPayout.Valid) },
        { nameof(RequestIncidentRefundCommand), new RequestIncidentRefundCommand(1, "Damaged", null, 100_000, TestPayout.Valid) },
        { nameof(WithdrawRefundCommand), new WithdrawRefundCommand(1) },
        { nameof(GetMyRefundsQuery), new GetMyRefundsQuery() },
        { nameof(GetOrderTrackingQuery), new GetOrderTrackingQuery(1) },
        { nameof(GetMyProfileQuery), new GetMyProfileQuery() },
        { nameof(UpdateMyProfileCommand), new UpdateMyProfileCommand("An", "0912345678") },
        { nameof(UpdateFcmTokenCommand), new UpdateFcmTokenCommand("token") },
        { nameof(DeleteMyAccountCommand), new DeleteMyAccountCommand() },
    };

    [Theory]
    [MemberData(nameof(CustomerRequests))]
    public async Task Signed_out_caller_is_unauthorized_on_every_customer_use_case_GB_50(string useCase, object request)
    {
        var app = new TestApp();

        var error = await Record.ExceptionAsync(() => app.Sender.Send(request));

        Assert.True(error is UnauthorizedException, $"{useCase} answered with {error?.GetType().Name ?? "success"} instead of UnauthorizedException");
        Assert.Empty(app.Orders.Items);
    }

    [Fact]
    public async Task Withdrawing_a_refund_that_does_not_exist_is_not_found_and_saves_nothing_UC_CUS_24()
    {
        var customer = User.Create("An", "0900000001", "an@x.vn", "h:secret123", UserRole.Customer, emailVerified: true);
        var app = TestApp.SignedInAs(customer);

        // Refund 999 does not exist for this customer: the use case must not reveal or touch other people's refunds.
        var error = await Record.ExceptionAsync(() => app.Sender.Send(new WithdrawRefundCommand(999)));

        Assert.IsType<NotFoundException>(error);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }
}
