using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Payments.Commands.CreateOrderPayment;
using ModMove.Application.Features.Payments.Commands.SyncOrderPayment;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>POST customer/Orders/{id}/payment/sync: learn of a PayOS payment by asking PayOS when the webhook did not come (UC-CUS-27).</summary>
public class SyncOrderPaymentTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private static (TestApp App, ShippingOrder Order) Build()
    {
        var customer = User.Create("An", "0900000001", "an@x.vn", "h", UserRole.Customer, emailVerified: true);
        var app = TestApp.SignedInAs(customer);
        var order = ShippingOrder.Create(
            "MM20261020001", customer.Id, vehicleTypeId: 1, couponId: null,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            12m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: 20_000,
            PaymentMethod.PayOS, payerRole: null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);
        order.CreatedAt = Now;
        app.Orders.Add(order);
        return (app, order);
    }

    [Fact]
    public async Task Order_becomes_paid_when_PayOS_says_the_code_is_paid_even_without_a_webhook()
    {
        var (app, order) = Build();
        await app.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.App));
        var transaction = Assert.Single(app.Payments.Items);
        app.Gateway.PaidCodes[transaction.PayosOrderCode!.Value] = 180_000m;
        app.Time.Now = Now.AddMinutes(2);

        await app.Sender.Send(new SyncOrderPaymentCommand(order.Id));

        Assert.Equal(PaymentTransactionStatus.Paid, transaction.TransactionStatus);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(Now.AddMinutes(2), order.PaidAt);
    }

    [Fact]
    public async Task Nothing_changes_while_PayOS_still_says_unpaid()
    {
        var (app, order) = Build();
        await app.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.App));
        var transaction = Assert.Single(app.Payments.Items);

        await app.Sender.Send(new SyncOrderPaymentCommand(order.Id));

        Assert.Equal(PaymentTransactionStatus.Pending, transaction.TransactionStatus);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
    }

    [Fact]
    public async Task A_paid_amount_that_differs_from_the_transaction_is_not_settled()
    {
        var (app, order) = Build();
        await app.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.App));
        var transaction = Assert.Single(app.Payments.Items);
        app.Gateway.PaidCodes[transaction.PayosOrderCode!.Value] = 1_000m;

        await app.Sender.Send(new SyncOrderPaymentCommand(order.Id));

        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
    }

    [Fact]
    public async Task Syncing_twice_settles_once()
    {
        var (app, order) = Build();
        await app.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.App));
        var transaction = Assert.Single(app.Payments.Items);
        app.Gateway.PaidCodes[transaction.PayosOrderCode!.Value] = 180_000m;
        await app.Sender.Send(new SyncOrderPaymentCommand(order.Id));
        var version = order.RowVersion;

        await app.Sender.Send(new SyncOrderPaymentCommand(order.Id));

        Assert.Equal(version, order.RowVersion);
    }
}
