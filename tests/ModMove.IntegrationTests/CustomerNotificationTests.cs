using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Notifications;
using ModMove.Application.Features.Orders.Commands.CancelOrder;
using ModMove.Application.Features.Orders.Commands.ConfirmReceipt;
using ModMove.Application.Features.Orders.Commands.ReviewPickupEvidence;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>DECISIONS B17, 8.3: the customer events, sent by whoever changes the state; B12: OrderStatusChanged.</summary>
public class CustomerNotificationTests
{
    private static INotificationService Service(TripSetup s, CapturingRealtimeNotifier realtime) =>
        new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<IUserRepository>(s.App.Users)
            .AddSingleton<IRealtimeNotifier>(realtime)
            .BuildServiceProvider()
            .GetRequiredService<INotificationService>();

    [Theory]
    [InlineData(CustomerEvent.OrderAccepted, "ORDER_ACCEPTED")]
    [InlineData(CustomerEvent.DriverArrived, "DRIVER_ARRIVED")]
    [InlineData(CustomerEvent.PickupPhotosToReview, "PICKUP_PHOTOS_TO_REVIEW")]
    [InlineData(CustomerEvent.OrderDelivered, "ORDER_DELIVERED")]
    [InlineData(CustomerEvent.OrderCompleted, "ORDER_COMPLETED")]
    [InlineData(CustomerEvent.IncidentResolved, "INCIDENT_RESOLVED")]
    [InlineData(CustomerEvent.RefundDecided, "REFUND_DECIDED")]
    [InlineData(CustomerEvent.OrderCancelled, "ORDER_CANCELLED")]
    public async Task Each_customer_event_of_8_3_reaches_the_customer_of_the_order(CustomerEvent customerEvent, string type)
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var realtime = new CapturingRealtimeNotifier();

        await Service(s, realtime).NotifyCustomerAsync(order, customerEvent, CancellationToken.None, detail: "Chi tiết");

        var sent = Assert.Single(realtime.Sent);
        Assert.Equal(("user", s.Customer.Id.ToString()), (sent.Target, sent.Id));
        var notification = Assert.IsType<Notification>(sent.Payload);
        Assert.Equal(type, notification.Type);
        Assert.False(string.IsNullOrWhiteSpace(notification.Title));
        Assert.Contains(order.OrderCode, notification.Body);
        Assert.Contains("Chi tiết", notification.Body);
        Assert.Equal(order.Id.ToString(), notification.Data!["orderId"]);
    }

    [Fact]
    public async Task Status_change_goes_to_the_order_followers_and_the_driver_B12()
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var realtime = new CapturingRealtimeNotifier();

        await Service(s, realtime).PublishOrderStatusAsync(order, s.Driver.Id, CancellationToken.None);

        Assert.Equal(
            [("order", order.Id.ToString(), RealtimeEvents.OrderStatusChanged), ("user", s.Driver.Id.ToString(), RealtimeEvents.OrderStatusChanged)],
            realtime.Sent.Select(x => (x.Target, x.Id, x.EventName)));
        var payload = Assert.IsType<OrderStatusUpdate>(realtime.Sent[0].Payload);
        Assert.Equal((order.Id, "PickedUp"), (payload.OrderId, payload.OrderStatus));
    }

    [Fact]
    public async Task Status_change_without_a_driver_goes_to_the_followers_only_and_a_failure_is_swallowed()
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var realtime = new CapturingRealtimeNotifier();
        await Service(s, realtime).PublishOrderStatusAsync(order, driverUserId: null, CancellationToken.None);
        Assert.Equal("order", Assert.Single(realtime.Sent).Target);

        var broken = new CapturingRealtimeNotifier { Fail = true };
        await Service(s, broken).PublishOrderStatusAsync(order, s.Driver.Id, CancellationToken.None);
        Assert.Empty(broken.Sent);
    }

    [Fact]
    public async Task A_failing_order_group_still_lets_the_driver_hear_the_status()
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var realtime = new CapturingRealtimeNotifier { FailOrderGroups = true };

        await Service(s, realtime).PublishOrderStatusAsync(order, s.Driver.Id, CancellationToken.None);

        Assert.Equal(("user", s.Driver.Id.ToString()), (Assert.Single(realtime.Sent).Target, realtime.Sent[0].Id));
    }

    // ---- Hiếu's own state changes publish OrderStatusChanged ----

    private static IEnumerable<(string Target, string? Id)> StatusEvents(TripSetup s) =>
        s.App.Realtime.Sent.Where(x => x.EventName == RealtimeEvents.OrderStatusChanged).Select(x => (x.Target, x.Id));

    [Fact]
    public async Task Cancelling_publishes_the_new_status()
    {
        var s = new TripSetup();
        var accepted = ModMove.Domain.Entities.ShippingOrder.Create(
            "MM20261020001", s.Customer.Id, 1, null, "A", 21m, 105m, "B", 21m, 105m, "Bình", "0912345678",
            12m, 30, false, 200_000, 0, 0, PaymentMethod.Cash, PayerRole.Sender,
            ModMove.Domain.Entities.CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), TripSetup.Now);
        accepted.Accept(s.Profile.Id, s.Driver.Id, TripSetup.Now);
        s.App.Orders.Add(accepted);

        await s.App.Sender.Send(new CancelOrderCommand(accepted.Id, "CHANGED_PLAN"));

        Assert.Equal([("order", accepted.Id.ToString()), ("user", s.Driver.Id.ToString())], StatusEvents(s));
    }

    [Fact]
    public async Task Approving_pickup_photos_publishes_InTransit()
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        await s.App.Sender.Send(new ReviewPickupEvidenceCommand(order.Id, true, null));

        Assert.Equal([("order", order.Id.ToString()), ("user", s.Driver.Id.ToString())], StatusEvents(s));
    }

    [Fact]
    public async Task Rejecting_pickup_photos_changes_no_status()
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        await s.App.Sender.Send(new ReviewPickupEvidenceCommand(order.Id, false, "Mờ"));

        Assert.Empty(StatusEvents(s));
    }

    [Fact]
    public async Task Confirming_receipt_publishes_Completed()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        await s.App.Sender.Send(new ConfirmReceiptCommand(order.Id));

        Assert.Equal([("order", order.Id.ToString()), ("user", s.Driver.Id.ToString())], StatusEvents(s));
    }
}
