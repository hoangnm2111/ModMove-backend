using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Incidents.Commands.CancelOrderByIncident;
using ModMove.Application.Features.Incidents.Commands.ReassignIncidentDriver;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByDispatcher;
using ModMove.Application.Features.Incidents.Commands.ResolveIncidentContinue;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// DECISIONS Q6b (2026-11-12): a Dispatcher opens an incident on a running order, so a driver can be reassigned or the order
/// cancelled when nobody reported anything. It does not hold the trip (7.6) and its text stays internal (8.3).
/// </summary>
public class DispatcherIncidentTests
{
    private const string Note = "Tài xế không nghe máy, khách gọi tổng đài";

    private static async Task<InTransitIncident> OpenAsync(DispatcherSetup s, ShippingOrder order)
    {
        var created = await s.App.Sender.Send(new ReportIncidentByDispatcherCommand(order.Id, IncidentType.Other, $"  {Note}  "));
        return s.App.Incidents.Items.Single(i => i.Id == created.IncidentId);
    }

    [Fact]
    public async Task Dispatcher_opens_an_incident_already_in_their_hands()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();

        var incident = await OpenAsync(s, order);

        Assert.Equal((s.Dispatcher.Id, IncidentResolutionStatus.InProgress, s.Dispatcher.Id),
            (incident.ReportedBy, incident.ResolutionStatus, incident.HandledBy));
        Assert.Equal(Note, incident.Description);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task The_customer_and_the_driver_get_only_the_generic_notice_8_3()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();

        await OpenAsync(s, order);

        var toCustomer = Assert.Single(s.SentTo(s.Customer));
        var toDriver = Assert.Single(s.SentTo(s.Driver));
        Assert.Equal(("INCIDENT_IN_PROGRESS", "INCIDENT_IN_PROGRESS"), (toCustomer.Type, toDriver.Type));
        Assert.DoesNotContain("nghe máy", toCustomer.Body);
        Assert.DoesNotContain("nghe máy", toDriver.Body);
    }

    [Fact]
    public async Task A_dispatcher_incident_does_not_hold_the_trip_7_6()
    {
        var s = new DispatcherSetup();
        var order = s.Accepted();

        await OpenAsync(s, order);

        // IncidentGuard asks exactly this: is there an open incident the current driver reported?
        Assert.False(await s.App.Incidents.HasOpenReportedByAsync(order.Id, s.Driver.Id));
    }

    [Fact]
    public async Task Reassign_on_a_dispatcher_incident_looks_near_the_vehicle_7_2()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        s.Profile.UpdateLocation(21.05m, 105.85m, DispatcherSetup.Now); // the vehicle, about 7 km from the pickup point
        var incident = await OpenAsync(s, order);
        var (_, newProfile, _) = s.ReplacementDriver(); // next to the vehicle, outside 5 km of the pickup point

        await s.App.Sender.Send(new ReassignIncidentDriverCommand(incident.Id, newProfile.Id, "Đổi tài xế"));

        Assert.Equal(newProfile.Id, order.DriverProfileId);
        Assert.Equal(IncidentDecision.ReassignDriver, incident.ResolutionDecision);
    }

    [Fact]
    public async Task A_dispatcher_incident_can_cancel_the_order()
    {
        var s = new DispatcherSetup();
        var order = s.Accepted(PaymentMethod.Cash);
        var incident = await OpenAsync(s, order);

        await s.App.Sender.Send(new CancelOrderByIncidentCommand(incident.Id, "Tài xế bỏ đơn", Refund: false, CargoDisposition: null));

        Assert.Equal(OrderStatus.Cancelled, order.OrderStatus);
    }

    [Fact]
    public async Task Another_dispatcher_cannot_decide_it_Q7()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var other = User.Create("Điều phối 2", "0900000099", "disp2@x.vn", "h", UserRole.Dispatcher, emailVerified: true);
        s.App.Users.Add(other);
        var incident = order.ReportIncidentByDispatcher(other.Id, IncidentType.Other, "Kiểm tra");
        s.App.Incidents.Add(incident);

        await Assert.ThrowsAsync<ConflictException>(() =>
            s.App.Sender.Send(new ResolveIncidentContinueCommand(incident.Id, "Đi tiếp")));
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Delivered)]
    public async Task Only_a_running_order_takes_a_dispatcher_incident(OrderStatus status)
    {
        var s = new DispatcherSetup();
        var order = status == OrderStatus.Delivered ? s.Delivered() : s.Accepted();
        if (status == OrderStatus.Pending)
        {
            order = ShippingOrder.Create(
                "MM20261020009", s.Customer.Id, 1, null, "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
                12m, 30, false, 200_000, 0, 0, PaymentMethod.Cash, PayerRole.Sender,
                CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), DispatcherSetup.Now);
            s.App.Orders.Add(order);
        }

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ReportIncidentByDispatcherCommand(order.Id, IncidentType.Other, "Kiểm tra")));

        Assert.Equal("INCIDENT_NOT_ALLOWED", ex.Code);
        Assert.Empty(s.App.Incidents.Items);
    }

    [Theory]
    [InlineData(0, "Kiểm tra")]
    [InlineData(1, "   ")]
    public async Task Bad_input_is_a_validation_error(long orderId, string description)
    {
        var s = new DispatcherSetup();

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new ReportIncidentByDispatcherCommand(orderId, IncidentType.Other, description)));
    }

    [Fact]
    public async Task A_description_longer_than_the_column_is_a_validation_error()
    {
        var s = new DispatcherSetup();

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new ReportIncidentByDispatcherCommand(1, IncidentType.Other, new string('a', 1001))));
    }

    [Fact]
    public async Task An_unknown_order_is_404()
    {
        var s = new DispatcherSetup();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.App.Sender.Send(new ReportIncidentByDispatcherCommand(999, IncidentType.Other, "Kiểm tra")));
    }
}
