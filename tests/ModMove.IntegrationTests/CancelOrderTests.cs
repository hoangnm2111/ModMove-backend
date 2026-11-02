using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Orders.Commands.CancelOrder;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>POST api/customer/orders/{id}/cancel (UC-CUS-29; DECISIONS 2.1-2.3, B3, B9, B11).</summary>
public class CancelOrderTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private sealed record Setup(TestApp App, User Customer, User Other, User Driver, DriverProfile Profile);

    private static Setup Build(AccountStatus status = AccountStatus.Active)
    {
        var customer = User.Create("An", "0900000001", "an@x.vn", "h", UserRole.Customer, emailVerified: true);
        var other = User.Create("Bình", "0900000002", "binh@x.vn", "h", UserRole.Customer, emailVerified: true);
        var driver = User.Create("Tài", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        customer.ChangeStatus(status);
        var app = TestApp.SignedInAs(customer, other, driver);
        var profile = DriverProfile.Create(driver.Id, "001", "B2-001", "front.jpg", "back.jpg");
        app.DriverProfiles.Add(profile);
        return new Setup(app, customer, other, driver, profile);
    }

    private static ShippingOrder AddOrder(Setup s, long customerId, PaymentMethod method = PaymentMethod.Cash, long? couponId = null)
    {
        var order = ShippingOrder.Create(
            "MM20261020001", customerId, vehicleTypeId: 1, couponId,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            12m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: 20_000,
            method, method == PaymentMethod.Cash ? PayerRole.Sender : null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);
        s.App.Orders.Add(order);
        return order;
    }

    private static CancelOrderCommand Cancel(ShippingOrder order) => new(order.Id, "CHANGED_PLAN", "Đổi ý");

    [Fact]
    public async Task PlaceOrder_cash_order_is_cancelled_with_nothing_to_refund_2_1()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        await s.App.Sender.Send(Cancel(order));

        Assert.Equal(OrderStatus.Cancelled, order.OrderStatus);
        Assert.Equal(("CHANGED_PLAN", "Đổi ý"), (order.CancellationReasonCode, order.CancellationReason));
        Assert.Empty(s.App.Refunds.Items);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Paid_PayOS_order_gets_a_full_cancellation_refund_that_goes_to_the_admin_B9()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id, PaymentMethod.PayOS);
        order.MarkPaid(Now);

        await s.App.Sender.Send(Cancel(order));

        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.Equal((order.Id, RefundSourceType.Cancellation, RefundStatus.Requested), (refund.OrderId, refund.SourceType, refund.RefundStatus));
        Assert.Equal(180_000m, refund.RequestedAmount);
        Assert.Equal(180_000m, refund.ProposedAmount);
        Assert.Null(refund.IncidentId);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Unpaid_PayOS_order_has_nothing_to_refund()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id, PaymentMethod.PayOS);

        await s.App.Sender.Send(Cancel(order));

        Assert.Empty(s.App.Refunds.Items);
    }

    [Fact]
    public async Task The_assigned_driver_is_told_when_an_accepted_order_is_cancelled_B17()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        order.Accept(s.Profile.Id, s.Driver.Id, Now);

        await s.App.Sender.Send(Cancel(order));

        var sent = Assert.Single(s.App.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal(("user", s.Driver.Id.ToString(), RealtimeEvents.NotificationReceived), (sent.Target, sent.Id, sent.EventName));
        var notification = Assert.IsType<Notification>(sent.Payload);
        Assert.Equal("ORDER_CANCELLED", notification.Type);
        Assert.Equal(order.Id.ToString(), notification.Data!["orderId"]);
    }

    [Fact]
    public async Task The_driver_is_told_even_when_the_customer_disconnects_right_after_the_save()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        order.Accept(s.Profile.Id, s.Driver.Id, Now);
        using var disconnect = new CancellationTokenSource();
        s.App.UnitOfWork.AfterSave = disconnect.Cancel;

        await s.App.Sender.Send(Cancel(order), disconnect.Token);

        var sent = Assert.Single(s.App.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal("ORDER_CANCELLED", Assert.IsType<Notification>(sent.Payload).Type);
    }

    [Fact]
    public async Task Paid_order_already_accepted_is_refunded_and_the_driver_told()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id, PaymentMethod.PayOS);
        order.MarkPaid(Now);
        order.Accept(s.Profile.Id, s.Driver.Id, Now);

        await s.App.Sender.Send(Cancel(order));

        Assert.Equal(180_000m, Assert.Single(s.App.Refunds.Items).ProposedAmount);
        Assert.Equal(s.Driver.Id.ToString(), Assert.Single(s.App.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived).Id);
    }

    [Fact]
    public async Task Cancelling_twice_does_not_create_a_second_refund()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id, PaymentMethod.PayOS);
        order.MarkPaid(Now);
        await s.App.Sender.Send(Cancel(order));

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Cancel(order)));

        Assert.Equal("ORDER_NOT_CANCELLABLE", error.Code);
        Assert.Single(s.App.Refunds.Items);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Nobody_is_notified_when_no_driver_has_the_order_yet()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        await s.App.Sender.Send(Cancel(order));

        Assert.DoesNotContain(s.App.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
    }

    [Fact]
    public async Task From_Arrived_on_the_customer_reports_an_incident_instead_2_2()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        order.Accept(s.Profile.Id, s.Driver.Id, Now);
        order.ConfirmArrival(s.Driver.Id, Now);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Cancel(order)));

        Assert.Equal("ORDER_NOT_CANCELLABLE", error.Code);
        Assert.Equal(OrderStatus.Arrived, order.OrderStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Another_customers_order_cannot_be_cancelled_C10()
    {
        var s = Build();
        var order = AddOrder(s, s.Other.Id);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Cancel(order)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Equal(OrderStatus.PlaceOrder, order.OrderStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
        Assert.Empty(s.App.Refunds.Items);
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        var s = Build();

        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new CancelOrderCommand(999, "CHANGED_PLAN")));

        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Locked_account_cannot_cancel_B8()
    {
        var s = Build(AccountStatus.Locked);
        var order = AddOrder(s, s.Customer.Id);

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(Cancel(order)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
        Assert.Equal(OrderStatus.PlaceOrder, order.OrderStatus);
    }

    [Fact]
    public async Task Signed_out_caller_is_unauthorized()
    {
        var app = new TestApp();

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new CancelOrderCommand(1, "CHANGED_PLAN")));
    }

    [Fact]
    public async Task Coupon_use_is_not_given_back_B11()
    {
        var s = Build();
        var coupon = Coupon.Create("WELCOME20K", 20_000, 100_000, Now.AddDays(-1), Now.AddYears(1), 10);
        s.App.Coupons.Add(coupon);
        coupon.MarkUsed();
        var order = AddOrder(s, s.Customer.Id, couponId: coupon.Id);

        await s.App.Sender.Send(Cancel(order));

        Assert.Equal(1, coupon.UsedCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Reason_code_is_required(string? code)
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new CancelOrderCommand(order.Id, code!, "Đổi ý")));
    }

    [Fact]
    public async Task Reason_is_at_most_500_characters()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new CancelOrderCommand(order.Id, "CHANGED_PLAN", new string('x', 501))));
    }

    [Fact]
    public async Task Code_alone_stores_the_name_from_the_list()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        await s.App.Sender.Send(new CancelOrderCommand(order.Id, "CHANGED_PLAN"));

        Assert.Equal(("CHANGED_PLAN", "Thay đổi kế hoạch"), (order.CancellationReasonCode, order.CancellationReason));
    }

    [Fact]
    public async Task Cancellation_refund_keeps_the_reason_code()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id, PaymentMethod.PayOS);
        order.MarkPaid(Now);

        await s.App.Sender.Send(new CancelOrderCommand(order.Id, "CHANGED_PLAN"));

        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.Equal(("CHANGED_PLAN", "Thay đổi kế hoạch"), (refund.ReasonCode, refund.Reason));
    }

    [Fact]
    public async Task Code_not_in_the_list_is_refused()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        var error = await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new CancelOrderCommand(order.Id, "NOPE")));

        Assert.Contains("ReasonCode", error.Errors.Keys);
        Assert.Equal(OrderStatus.PlaceOrder, order.OrderStatus);
    }

    [Fact]
    public async Task Code_switched_off_by_the_admin_is_refused()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        s.App.MasterData.Items.Single(i => i.Code == "CHANGED_PLAN").SetActive(false);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new CancelOrderCommand(order.Id, "CHANGED_PLAN")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Other_needs_a_written_reason(string? reason)
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new CancelOrderCommand(order.Id, "OTHER", reason)));
    }

    [Theory]
    [InlineData("other")]
    [InlineData("ÓTHER")] // the database column ignores case and accents
    public async Task Other_spelled_differently_still_needs_a_written_reason(string code)
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        var error = await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new CancelOrderCommand(order.Id, code)));

        Assert.Contains("Reason", error.Errors.Keys);
        Assert.Equal(OrderStatus.PlaceOrder, order.OrderStatus);
    }

    [Fact]
    public async Task Code_is_stored_as_the_list_spells_it()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        await s.App.Sender.Send(new CancelOrderCommand(order.Id, " changed_plan "));

        Assert.Equal("CHANGED_PLAN", order.CancellationReasonCode);
    }

    [Fact]
    public async Task Other_with_a_written_reason_is_accepted()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        await s.App.Sender.Send(new CancelOrderCommand(order.Id, "OTHER", " Chuyển ngày "));

        Assert.Equal(("OTHER", "Chuyển ngày"), (order.CancellationReasonCode, order.CancellationReason));
    }
}
