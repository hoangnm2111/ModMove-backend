using ModMove.Application.Features.Payments.Commands.CancelOrderPayment;
using ModMove.Application.Features.Payments.Commands.CreateOrderPayment;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>POST api/customer/orders/{id}/payment/cancel (UC-CUS-27 4a): the customer closes the PayOS page.</summary>
public class CancelOrderPaymentTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private static (TestApp App, User Customer, User Other, ShippingOrder Order) Build()
    {
        var customer = User.Create("An", "0900000001", "an@x.vn", "h", UserRole.Customer, emailVerified: true);
        var other = User.Create("Bình", "0900000002", "binh@x.vn", "h", UserRole.Customer, emailVerified: true);
        var app = TestApp.SignedInAs(customer, other);
        var order = ShippingOrder.Create(
            "MM20261020001", customer.Id, vehicleTypeId: 1, couponId: null,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            12m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: 20_000,
            PaymentMethod.PayOS, null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);
        order.CreatedAt = Now;
        app.Orders.Add(order);
        return (app, customer, other, order);
    }

    [Fact]
    public async Task Cancels_the_live_session_here_and_at_PayOS_and_keeps_the_order_pending()
    {
        var (app, _, _, order) = Build();
        await app.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var code = app.Payments.Items[0].PayosOrderCode!.Value;
        app.Time.Now = Now.AddMinutes(2);

        await app.Sender.Send(new CancelOrderPaymentCommand(order.Id));

        Assert.Equal(PaymentTransactionStatus.Cancelled, app.Payments.Items[0].TransactionStatus);
        Assert.Equal([code], app.Gateway.Cancelled);
        Assert.Equal((OrderStatus.Pending, PaymentStatus.Unpaid), (order.OrderStatus, order.PaymentStatus));
    }

    [Fact]
    public async Task The_customer_can_pay_again_after_cancelling_the_session_3_1()
    {
        var (app, _, _, order) = Build();
        await app.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        await app.Sender.Send(new CancelOrderPaymentCommand(order.Id));

        await app.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));

        Assert.Equal(PaymentTransactionStatus.Pending, app.Payments.Items[1].TransactionStatus);
    }

    [Fact]
    public async Task Nothing_pending_does_nothing()
    {
        var (app, _, _, order) = Build();

        await app.Sender.Send(new CancelOrderPaymentCommand(order.Id));

        Assert.Empty(app.Gateway.Cancelled);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task An_expired_session_is_not_sent_to_PayOS()
    {
        var (app, _, _, order) = Build();
        await app.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        app.Time.Now = Now.AddMinutes(11);

        await app.Sender.Send(new CancelOrderPaymentCommand(order.Id));

        Assert.Equal(PaymentTransactionStatus.Cancelled, app.Payments.Items[0].TransactionStatus);
        Assert.Empty(app.Gateway.Cancelled);
    }

    [Fact]
    public async Task Another_customers_order_is_refused_C10()
    {
        var (app, _, other, _) = Build();
        var othersOrder = ShippingOrder.Create(
            "MM20261020002", other.Id, vehicleTypeId: 1, couponId: null,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            12m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: 0,
            PaymentMethod.PayOS, null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);
        app.Orders.Add(othersOrder);
        var session = PaymentTransaction.ForOrderViaPayOs(othersOrder.Id, 200_000, 123, "https://pay", Now);
        app.Payments.Add(session);

        var error = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new CancelOrderPaymentCommand(othersOrder.Id)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Equal(PaymentTransactionStatus.Pending, session.TransactionStatus);
    }
}
