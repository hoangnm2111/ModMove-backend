using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;
using ModMove.Application.Features.Incidents.Queries.GetMyIncidents;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>POST api/customer/incidents (UC-CUS-15; DECISIONS 2.2, 7.1, C10).</summary>
public class ReportIncidentByCustomerTests
{
    private const string Photo = "https://res.cloudinary.com/demo/image/upload/modmove/customers/1/a.jpg";

    [Fact]
    public async Task Customer_reports_an_incident_and_dispatchers_and_the_driver_are_told_GB17()
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        var result = await s.App.Sender.Send(new ReportIncidentByCustomerCommand(
            order.Id, IncidentType.Other, " Tài xế đi sai đường ", [Photo], 21.0m, 105.8m));

        var incident = Assert.Single(s.App.Incidents.Items);
        Assert.Equal(incident.Id, result.IncidentId);
        Assert.Equal((order.Id, s.Customer.Id, "Tài xế đi sai đường"), (incident.OrderId, incident.ReportedBy, incident.Description));
        Assert.Equal([Photo], incident.PhotoUrls!);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
        var dispatchers = Assert.Single(s.App.Realtime.Sent, x => x.Target == "dispatchers");
        Assert.Equal("INCIDENT_REPORTED", Assert.IsType<Notification>(dispatchers.Payload).Type);
        var driver = Assert.Single(s.App.Realtime.Sent, x => x.Target == "user");
        Assert.Equal(s.Driver.Id.ToString(), driver.Id);
        var notification = Assert.IsType<Notification>(driver.Payload);
        Assert.Equal("INCIDENT_REPORTED", notification.Type);
        Assert.Equal((order.Id.ToString(), incident.Id.ToString()), (notification.Data!["orderId"], notification.Data["incidentId"]));
    }

    [Fact]
    public async Task The_driver_is_not_shown_what_the_customer_wrote()
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        await s.App.Sender.Send(new ReportIncidentByCustomerCommand(
            order.Id, IncidentType.Other, "Tài xế thái độ không tốt", null, null, null));

        var notification = Assert.IsType<Notification>(Assert.Single(s.App.Realtime.Sent, x => x.Target == "user").Payload);
        Assert.Contains(order.OrderCode, notification.Title);
        Assert.DoesNotContain("thái độ", notification.Title + notification.Body);
        var dispatchers = Assert.IsType<Notification>(Assert.Single(s.App.Realtime.Sent, x => x.Target == "dispatchers").Payload);
        Assert.Equal("Tài xế thái độ không tốt", dispatchers.Body);
    }

    [Fact]
    public async Task After_delivery_the_customer_files_a_complaint_instead_7_3()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ReportIncidentByCustomerCommand(order.Id, IncidentType.DamagedCargo, "Vỡ", null, null, null)));

        Assert.Equal("INCIDENT_NOT_ALLOWED", error.Code);
        Assert.Empty(s.App.Incidents.Items);
    }

    [Fact]
    public async Task Another_customers_order_is_refused_C10()
    {
        var s = new TripSetup();
        var order = s.PickedUp(customerId: s.Other.Id);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ReportIncidentByCustomerCommand(order.Id, IncidentType.Other, "x", null, null, null)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Photos_are_at_most_five_from_our_storage()
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ReportIncidentByCustomerCommand(
            order.Id, IncidentType.Other, "x", Enumerable.Repeat(Photo, 6).ToList(), null, null)));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ReportIncidentByCustomerCommand(
            order.Id, IncidentType.Other, "x", ["https://evil.example/a.jpg"], null, null)));
    }

    [Theory]
    [InlineData("", 21.0, 105.8)]
    [InlineData("x", 91.0, 105.8)]
    [InlineData("x", 21.0, 181.0)]
    public async Task Description_is_required_and_coordinates_are_real(string description, double lat, double lng)
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ReportIncidentByCustomerCommand(
            order.Id, IncidentType.Other, description, null, (decimal)lat, (decimal)lng)));
    }

    [Theory]
    [InlineData(10.7769, null)]
    [InlineData(null, 106.7009)]
    public async Task Coordinates_come_as_a_pair_or_not_at_all(double? lat, double? lng)
    {
        // A lone latitude would be paired with the pickup longitude when the Dispatcher looks for a replacement driver.
        var s = new TripSetup();
        var order = s.PickedUp();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ReportIncidentByCustomerCommand(
            order.Id, IncidentType.Other, "Xe hỏng giữa đường", null, (decimal?)lat, (decimal?)lng)));
    }

    [Fact]
    public async Task Unknown_order_is_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new TripSetup().App.Sender.Send(
            new ReportIncidentByCustomerCommand(999, IncidentType.Other, "x", null, null, null)));

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var s = new TripSetup(AccountStatus.Locked);
        var order = s.PickedUp();

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            s.App.Sender.Send(new ReportIncidentByCustomerCommand(order.Id, IncidentType.Other, "x", null, null, null)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }
}

/// <summary>GET api/customer/incidents (UC-CUS-16); rows served by FakeSql.</summary>
public class MyIncidentsQueryTests
{
    private const long CustomerId = 7;

    private sealed class Db
    {
        public FakeSql Sql { get; } = new();
        public string AccountStatus { get; set; } = "Active";
        public long ReportedBy { get; set; } = CustomerId;
        public string? PhotoUrls { get; set; }
        public List<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> Executed { get; } = [];

        public Db()
        {
            Sql.ScalarResult = 1;
            Sql.CustomQuery = (text, parameters) =>
            {
                Executed.Add((text, parameters));
                if (text.Contains("FROM users"))
                    return Table(("AccountStatus", typeof(string), AccountStatus));
                return Table(
                    ("Id", typeof(long), 5L), ("OrderId", typeof(long), 1L), ("OrderCode", typeof(string), "MM20261020001"),
                    ("IncidentType", typeof(string), "VehicleBreakdown"), ("Description", typeof(string), "Xe hỏng"),
                    ("PhotoUrls", typeof(string), PhotoUrls), ("ReportedBy", typeof(long), ReportedBy),
                    ("ResolutionStatus", typeof(string), "Resolved"), ("ResolutionDecision", typeof(string), "ReassignDriver"),
                    ("ResolutionNote", typeof(string), "Đã đổi tài xế"), ("ResolvedAt", typeof(DateTime), new DateTime(2026, 10, 20, 10, 0, 0)),
                    ("CreatedAt", typeof(DateTime), new DateTime(2026, 10, 20, 9, 0, 0)));
            };
        }

        public ISender Sender() => new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(Sql)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(CustomerId))
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

    [Fact]
    public async Task Lists_incidents_on_the_callers_orders_with_the_dispatchers_decision()
    {
        var db = new Db { ReportedBy = 99 };

        var page = await db.Sender().Send(new GetMyIncidentsQuery());

        var (sql, parameters) = db.Executed.Last(e => e.Sql.Contains("FROM in_transit_incidents"));
        Assert.Contains("o.customer_id = @CustomerId", sql);
        Assert.Equal(CustomerId, parameters["CustomerId"]);
        var item = Assert.Single(page.Items);
        Assert.Equal(("VehicleBreakdown", "Resolved", "ReassignDriver", "Đã đổi tài xế"),
            (item.IncidentType, item.ResolutionStatus, item.ResolutionDecision, item.ResolutionNote));
        Assert.False(item.ReportedByYou); // the driver reported it
        Assert.Empty(item.PhotoUrls);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero), item.ResolvedAt);
    }

    [Fact]
    public async Task Customer_sees_the_drivers_own_words_and_photos_8_3()
    {
        var db = new Db { ReportedBy = 99, PhotoUrls = """["https://res.cloudinary.com/demo/x.jpg"]""" };

        var item = (await db.Sender().Send(new GetMyIncidentsQuery())).Items.Single();

        Assert.False(item.ReportedByYou);
        Assert.Equal("Xe hỏng", item.Description);
        Assert.Equal(["https://res.cloudinary.com/demo/x.jpg"], item.PhotoUrls);
    }

    [Fact]
    public async Task Filters_by_order_and_status_as_parameters()
    {
        var db = new Db();

        await db.Sender().Send(new GetMyIncidentsQuery(OrderId: 12, Status: IncidentResolutionStatus.InProgress));

        var (sql, parameters) = db.Executed.Last(e => e.Sql.Contains("FROM in_transit_incidents"));
        Assert.Contains("(@OrderId IS NULL OR i.order_id = @OrderId)", sql);
        Assert.Contains("(@Status IS NULL OR i.resolution_status = @Status)", sql);
        Assert.Equal(12L, parameters["OrderId"]);
        Assert.Equal("InProgress", parameters["Status"]);
    }

    [Fact]
    public async Task Without_filters_both_are_null()
    {
        var db = new Db();

        await db.Sender().Send(new GetMyIncidentsQuery());

        var (_, parameters) = db.Executed.Last(e => e.Sql.Contains("FROM in_transit_incidents"));
        // Dapper sends a null parameter as DBNull.
        Assert.IsType<DBNull>(parameters["OrderId"]);
        Assert.IsType<DBNull>(parameters["Status"]);
    }

    [Fact]
    public async Task Order_id_must_be_positive() =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetMyIncidentsQuery(OrderId: 0)));

    [Fact]
    public async Task Unknown_status_is_refused() =>
        await Assert.ThrowsAsync<ValidationException>(() =>
            new Db().Sender().Send(new GetMyIncidentsQuery(Status: (IncidentResolutionStatus)42)));

    [Fact]
    public async Task Marks_the_incidents_the_customer_reported() =>
        Assert.True((await new Db().Sender().Send(new GetMyIncidentsQuery())).Items.Single().ReportedByYou);

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => new Db { AccountStatus = "Locked" }.Sender().Send(new GetMyIncidentsQuery()));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 51)]
    public async Task Paging_is_bounded(int page, int pageSize) =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetMyIncidentsQuery(page, pageSize)));
}
