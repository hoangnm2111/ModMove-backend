using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Payments.Commands.CreateOrderPayment;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>POST api/customer/orders/{id}/payment (UC-CUS-27; DECISIONS 3.1, B10; GB-13).</summary>
public class CreateOrderPaymentTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private sealed record Setup(TestApp App, User Customer, User Other);

    private static Setup Build(AccountStatus status = AccountStatus.Active)
    {
        var customer = User.Create("An", "0900000001", "an@x.vn", "h", UserRole.Customer, emailVerified: true);
        var other = User.Create("Bình", "0900000002", "binh@x.vn", "h", UserRole.Customer, emailVerified: true);
        customer.ChangeStatus(status);
        return new Setup(TestApp.SignedInAs(customer, other), customer, other);
    }

    // total_fee = 200,000 - 20,000 = 180,000; the database stamps CreatedAt on save, so the test does.
    private static ShippingOrder AddOrder(Setup s, long customerId, PaymentMethod method = PaymentMethod.PayOS)
    {
        var order = ShippingOrder.Create(
            "MM20261020001", customerId, vehicleTypeId: 1, couponId: null,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            12m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: 20_000,
            method, method == PaymentMethod.Cash ? PayerRole.Sender : null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);
        order.CreatedAt = Now;
        s.App.Orders.Add(order);
        return order;
    }

    private static CreateOrderPaymentCommand Pay(ShippingOrder order, PaymentPlatform platform = PaymentPlatform.Web) => new(order.Id, platform);

    [Fact]
    public async Task Creates_a_PayOS_session_for_the_order_total_valid_10_minutes_GB13()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        var result = await s.App.Sender.Send(Pay(order));

        var transaction = Assert.Single(s.App.Payments.Items);
        Assert.Equal((order.Id, PaymentMethod.PayOS, PaymentTransactionStatus.Pending), (transaction.OrderId, transaction.PaymentMethod, transaction.TransactionStatus));
        Assert.Equal(180_000m, transaction.Amount);
        Assert.Equal(Now.AddMinutes(10), transaction.ExpiresAt);
        Assert.Null(transaction.WalletId);

        Assert.Equal(new CreateOrderPaymentResponse(
            transaction.Id, $"https://pay.payos.vn/web/{transaction.PayosOrderCode}", $"qr-{transaction.PayosOrderCode}", 180_000m, Now.AddMinutes(10)), result);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus); // only the webhook marks it paid
    }

    [Fact]
    public async Task Asks_PayOS_for_the_amount_a_short_description_the_platform_and_the_same_expiry()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        await s.App.Sender.Send(Pay(order, PaymentPlatform.App));

        var asked = Assert.Single(s.App.Gateway.Created);
        Assert.Equal((180_000m, $"MM{order.Id}", PaymentPlatform.App, Now.AddMinutes(10)), (asked.Amount, asked.Description, asked.Platform, asked.ExpiresAt));
        Assert.Equal(s.App.Payments.Items[0].PayosOrderCode, asked.Code);
        Assert.InRange(asked.Description.Length, 1, 9); // PayOS non-linked bank limit
    }

    [Fact]
    public async Task The_PayOS_order_code_is_a_whole_number_PayOS_accepts()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        await s.App.Sender.Send(Pay(order));

        var code = s.App.Payments.Items[0].PayosOrderCode!.Value;
        Assert.InRange(code, 1, 9_007_199_254_740_991); // 2^53 - 1
    }

    [Fact]
    public async Task A_new_code_after_the_old_one_expired_replaces_it_3_1()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        await s.App.Sender.Send(Pay(order));
        s.App.Time.Now = Now.AddMinutes(11);

        var second = await s.App.Sender.Send(Pay(order));

        Assert.Equal(2, s.App.Payments.Items.Count);
        Assert.Equal(PaymentTransactionStatus.Cancelled, s.App.Payments.Items[0].TransactionStatus); // old code takes no more transfers
        Assert.Equal(PaymentTransactionStatus.Pending, s.App.Payments.Items[1].TransactionStatus);
        Assert.Equal(s.App.Payments.Items[1].Id, second.TransactionId);
        Assert.NotEqual(s.App.Payments.Items[0].PayosOrderCode, s.App.Payments.Items[1].PayosOrderCode);
        Assert.Empty(s.App.Gateway.Cancelled); // already dead at PayOS, nothing to cancel there
    }

    [Fact]
    public async Task A_new_code_while_the_old_one_still_lives_cancels_the_old_one_at_PayOS_too()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        await s.App.Sender.Send(Pay(order));
        var oldCode = s.App.Payments.Items[0].PayosOrderCode!.Value;
        s.App.Time.Now = Now.AddMinutes(3);

        await s.App.Sender.Send(Pay(order));

        Assert.Equal(PaymentTransactionStatus.Cancelled, s.App.Payments.Items[0].TransactionStatus);
        Assert.Equal([oldCode], s.App.Gateway.Cancelled);
    }

    [Fact]
    public async Task A_paid_old_session_is_left_alone()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        await s.App.Sender.Send(Pay(order));
        s.App.Payments.Items[0].MarkPaid(Now.AddMinutes(1));
        s.App.Time.Now = Now.AddMinutes(2);

        await s.App.Sender.Send(Pay(order));

        Assert.Equal(PaymentTransactionStatus.Paid, s.App.Payments.Items[0].TransactionStatus);
        Assert.Empty(s.App.Gateway.Cancelled);
    }

    [Fact]
    public async Task Another_customers_order_is_refused_C10_and_PayOS_is_not_asked()
    {
        var s = Build();
        var order = AddOrder(s, s.Other.Id);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Pay(order)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Empty(s.App.Gateway.Created);
        Assert.Empty(s.App.Payments.Items);
    }

    [Fact]
    public async Task A_cash_order_has_nothing_to_pay_online()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id, PaymentMethod.Cash);

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Pay(order)));

        Assert.Empty(s.App.Gateway.Created);
    }

    [Fact]
    public async Task An_order_already_paid_is_refused()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        order.MarkPaid(Now);

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Pay(order)));

        Assert.Empty(s.App.Gateway.Created);
    }

    [Fact]
    public async Task A_cancelled_order_is_refused_with_ORDER_CANCELLED()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        order.Cancel("changed my mind", ChangedByRole.Customer, s.Customer.Id, Now);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Pay(order)));

        Assert.Equal("ORDER_CANCELLED", error.Code);
        Assert.Empty(s.App.Gateway.Created);
    }

    [Fact]
    public async Task Thirty_minutes_after_booking_it_is_too_late_3_1()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);

        s.App.Time.Now = Now.AddMinutes(29).AddSeconds(59);
        await s.App.Sender.Send(Pay(order)); // last second still fine

        s.App.Time.Now = Now.AddMinutes(30);
        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Pay(order)));
        Assert.Single(s.App.Gateway.Created);
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        var s = Build();

        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new CreateOrderPaymentCommand(999, PaymentPlatform.Web)));
    }

    [Fact]
    public async Task An_inactive_account_is_refused_even_with_a_live_token_B8()
    {
        var s = Build(AccountStatus.Inactive);
        var order = AddOrder(s, s.Customer.Id);

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(Pay(order)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Theory]
    [InlineData(0, PaymentPlatform.Web)]
    [InlineData(-1, PaymentPlatform.App)]
    [InlineData(1, (PaymentPlatform)7)]
    public async Task Bad_input_is_a_validation_error(long orderId, PaymentPlatform platform)
    {
        var s = Build();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new CreateOrderPaymentCommand(orderId, platform)));
    }

    [Fact]
    public async Task When_PayOS_is_down_nothing_is_saved_and_the_old_code_is_kept()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        await s.App.Sender.Send(Pay(order));
        s.App.Time.Now = Now.AddMinutes(3);
        s.App.Gateway.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => s.App.Sender.Send(Pay(order)));

        Assert.Single(s.App.Payments.Items);
        Assert.Equal(PaymentTransactionStatus.Pending, s.App.Payments.Items[0].TransactionStatus);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
        Assert.Empty(s.App.Gateway.Cancelled);
    }

    [Fact]
    public async Task A_clash_on_the_unique_PayOS_code_is_a_409_the_client_can_retry()
    {
        var s = Build();
        var order = AddOrder(s, s.Customer.Id);
        s.App.UnitOfWork.ThrowConflictOnSave = true;

        await Assert.ThrowsAsync<ConflictException>(() => s.App.Sender.Send(Pay(order)));
    }
}

