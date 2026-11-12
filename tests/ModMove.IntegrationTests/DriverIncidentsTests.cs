using System.Data;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByDriver;
using ModMove.Application.Features.Incidents.Queries.GetDriverIncidents;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// Driver incident reporting &amp; viewing: UC-DRV-18, 19; DECISIONS 6.1, 7.1, 7.2, 8.3.
/// </summary>
public class DriverIncidentsTests
{
    private const string Photo = "https://res.cloudinary.com/demo/image/upload/modmove/drivers/1/inc.jpg";

    private sealed record Setup(TestApp App, User Driver, User Customer, DriverProfile Profile, ShippingOrder Order);

    private static Setup Build(OrderStatus target = OrderStatus.InTransit)
    {
        var driver = User.Create("Nguyễn Văn Tài", "0912000010", "drv10@x.vn", "h:x", UserRole.Driver, emailVerified: true);
        var customer = User.Create("Trần Thị Khách", "0909000010", "cus10@x.vn", "h:x", UserRole.Customer, emailVerified: true);
        customer.SetFcmToken("fcm-cus-10");
        var app = TestApp.SignedInAs(driver, customer);

        var profile = DriverProfile.Create(driver.Id, "079000000010", "79000010", Photo, Photo);
        app.DriverProfiles.Add(profile);
        profile.Approve(Vehicle.Create(profile.Id, 1, "51C-888.88", 1000, "200x150x150", Photo, Photo, Photo));
        profile.UpdateLocation(10.7950m, 106.7210m, TestApp.Start);

        var order = ShippingOrder.Create(
            "MM20261108010", customer.Id, 1, null,
            "A", 10.77m, 106.70m, "B", 10.84m, 106.78m, "Người Nhận", "0988111222",
            10m, 25, false, 180_000m, 0m, 0m, PaymentMethod.PayOS, null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, [Photo], null), TestApp.Start);
        order.MarkPaid(TestApp.Start);

        if (target >= OrderStatus.Accepted)
            order.Accept(profile.Id, driver.Id, TestApp.Start);
        if (target >= OrderStatus.Arrived)
            order.ConfirmArrival(driver.Id, TestApp.Start);
        if (target >= OrderStatus.PickedUp)
            order.ConfirmPickup([Photo, Photo], driver.Id, TestApp.Start);
        if (target >= OrderStatus.InTransit)
            order.ApprovePickupEvidence(customer.Id, TestApp.Start);
        if (target >= OrderStatus.Delivered)
            order.Deliver(Photo, driver.Id, TestApp.Start);

        app.Orders.Add(order);
        return new Setup(app, driver, customer, profile, order);
    }

    [Fact]
    public async Task Driver_reports_incident_falls_back_to_last_gps_and_alerts_dispatchers_and_customer_DRV18()
    {
        var s = Build(OrderStatus.Accepted); // DECISIONS 6.1: allowed right from Accepted since drivers cannot self-cancel

        var created = await s.App.Sender.Send(new ReportIncidentByDriverCommand(
            s.Order.Id,
            IncidentType.VehicleBreakdown,
            "  Xe bị nổ lốp trên đường đến điểm lấy  ",
            [Photo]));

        var saved = Assert.Single(s.App.Incidents.Items);
        Assert.Equal(created.IncidentId, saved.Id);
        Assert.Equal("Xe bị nổ lốp trên đường đến điểm lấy", saved.Description);
        Assert.Equal((10.7950m, 106.7210m), (saved.Latitude, saved.Longitude)); // fallback from DriverProfile
        Assert.Equal(1, s.App.UnitOfWork.Saves);

        var dispatcherAlert = Assert.Single(s.App.Realtime.Sent, m => m.Target == "dispatchers");
        Assert.Equal("INCIDENT_REPORTED", Assert.IsType<Notification>(dispatcherAlert.Payload).Type);

        var customerAlert = Assert.Single(s.App.Realtime.Sent, m => m.Target == "user" && m.Id == s.Customer.Id.ToString());
        Assert.Equal("INCIDENT_REPORTED", Assert.IsType<Notification>(customerAlert.Payload).Type);
        Assert.Single(s.App.Push.Sent);
    }

    [Fact]
    public async Task Driver_cannot_report_incident_once_order_is_delivered()
    {
        var s = Build(OrderStatus.Delivered);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(
            new ReportIncidentByDriverCommand(s.Order.Id, IncidentType.DamagedCargo, "Hàng trầy")));

        Assert.Equal("INCIDENT_NOT_ALLOWED", error.Code);
        Assert.Empty(s.App.Incidents.Items);
    }

    [Fact]
    public async Task Driver_views_incident_list_and_detail_with_customer_description_masked_DRV19()
    {
        var s = Build();

        var ctx = new DataTable();
        ctx.Columns.Add("AccountStatus", typeof(string));
        ctx.Columns.Add("DriverProfileId", typeof(long));
        ctx.Columns.Add("ApprovalStatus", typeof(string));
        ctx.Columns.Add("BankName", typeof(string));
        ctx.Columns.Add("BankAccountNumber", typeof(string));
        ctx.Columns.Add("BankAccountHolder", typeof(string));
        ctx.Rows.Add("Active", s.Profile.Id, "Approved", null, null, null);

        var rows = new DataTable();
        rows.Columns.Add("Id", typeof(long));
        rows.Columns.Add("OrderId", typeof(long));
        rows.Columns.Add("OrderCode", typeof(string));
        rows.Columns.Add("OrderStatus", typeof(string));
        rows.Columns.Add("IncidentType", typeof(string));
        rows.Columns.Add("Description", typeof(string));
        rows.Columns.Add("PhotoUrls", typeof(string));
        rows.Columns.Add("Latitude", typeof(decimal));
        rows.Columns.Add("Longitude", typeof(decimal));
        rows.Columns.Add("ReportedBy", typeof(long));
        rows.Columns.Add("ResolutionStatus", typeof(string));
        rows.Columns.Add("ResolutionDecision", typeof(string));
        rows.Columns.Add("ResolutionNote", typeof(string));
        rows.Columns.Add("CreatedAt", typeof(DateTime));
        rows.Columns.Add("ResolvedAt", typeof(DateTime));

        rows.Rows.Add(11L, s.Order.Id, s.Order.OrderCode, "Accepted", "VehicleBreakdown",
            "Xe hỏng động cơ", "[\"https://res.cloudinary.com/demo/1.jpg\"]", 10.79m, 106.72m,
            s.Driver.Id, "Resolved", "ReassignDriver", "Đã chuyển đơn cho tài xế khác",
            TestApp.Start.UtcDateTime, TestApp.Start.AddMinutes(5).UtcDateTime);
        rows.Rows.Add(12L, s.Order.Id, s.Order.OrderCode, "InTransit", "Other",
            "Phàn nàn riêng tư của khách", "[\"https://res.cloudinary.com/demo/private.jpg\"]", 10.80m, 106.73m,
            s.Customer.Id, "Open", null, null, TestApp.Start.UtcDateTime, DBNull.Value);

        s.App.Sql.Results.Enqueue(ctx);
        s.App.Sql.ScalarResult = 2;
        s.App.Sql.Results.Enqueue(rows);

        var page = await s.App.Sender.Send(new GetDriverIncidentsQuery());
        Assert.Equal(2, page.TotalCount);
        Assert.True(page.Items[0].ReportedByYou);
        Assert.Equal("Xe hỏng động cơ", page.Items[0].Description);
        Assert.Equal("ReassignDriver", page.Items[0].ResolutionDecision);

        Assert.False(page.Items[1].ReportedByYou);
        Assert.DoesNotContain("riêng tư", page.Items[1].Description);
        // Q6b: it may be a Dispatcher's or a previous driver's report, so the notice does not name the customer.
        Assert.Equal("Có sự cố trên đơn này, Điều phối viên đang xử lý.", page.Items[1].Description);
        Assert.Empty(page.Items[1].PhotoUrls);
        Assert.Null(page.Items[1].Latitude);

        // Detail not found -> 404
        s.App.Sql.Results.Enqueue(ctx);
        s.App.Sql.Results.Enqueue(rows.Clone());
        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new GetDriverIncidentDetailQuery(999)));
    }
}

