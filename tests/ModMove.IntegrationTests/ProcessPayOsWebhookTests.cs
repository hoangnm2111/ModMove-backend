using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Payments.Commands.CreateOrderPayment;
using ModMove.Application.Features.Payments.Commands.ProcessPayOsWebhook;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;
using ModMove.Infrastructure.ExternalServices.PayOS;

namespace ModMove.IntegrationTests;

/// <summary>POST api/webhooks/payos (UC-CUS-27, UC-DRV-21; DECISIONS 3.1, 3.2, B10).</summary>
public class ProcessPayOsWebhookTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private sealed record Setup(TestApp App, User Customer);

    private static Setup Build()
    {
        var customer = User.Create("An", "0900000001", "an@x.vn", "h", UserRole.Customer, emailVerified: true);
        return new Setup(TestApp.SignedInAs(customer), customer);
    }

    private static ShippingOrder AddOrder(Setup s)
    {
        var order = ShippingOrder.Create(
            "MM20261020001", s.Customer.Id, vehicleTypeId: 1, couponId: null,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            12m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: 20_000,
            PaymentMethod.PayOS, payerRole: null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);
        order.CreatedAt = Now;
        s.App.Orders.Add(order);
        return order;
    }

    private static string WebhookJson(
        long orderCode,
        decimal amount = 180_000m,
        string signature = FakePaymentGateway.ValidSignature,
        string code = "00",
        bool success = true) =>
        $$"""
        {
          "code": "{{code}}",
          "desc": "success",
          "success": {{(success ? "true" : "false")}},
          "data": {
            "orderCode": {{orderCode}},
            "amount": {{amount:0}},
            "description": "ModMove MM20261020001",
            "accountNumber": "0123456789",
            "reference": "FT26293",
            "currency": "VND",
            "code": "{{code}}"
          },
          "signature": "{{signature}}"
        }
        """;

    [Fact]
    public async Task Valid_webhook_marks_the_transaction_and_order_paid_and_starts_matching_timer_3_1_3_2()
    {
        var s = Build();
        var order = AddOrder(s);
        await s.App.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var transaction = Assert.Single(s.App.Payments.Items);
        var beforeVersion = order.RowVersion;
        var paidTime = Now.AddMinutes(2);
        s.App.Time.Now = paidTime;

        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(transaction.PayosOrderCode!.Value, 180_000m)));

        Assert.Equal(PaymentTransactionStatus.Paid, transaction.TransactionStatus);
        Assert.Equal(paidTime, transaction.PaidAt);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(paidTime, order.PaidAt);
        Assert.Equal(beforeVersion + 1, order.RowVersion);
        Assert.Equal(2, s.App.UnitOfWork.Saves); // 1 for CreateOrderPayment + 1 for webhook
    }

    [Fact]
    public async Task Valid_webhook_for_order_payment_broadcasts_NEW_MATCHING_ORDER_to_nearby_eligible_drivers_8_3()
    {
        var s = Build();
        var order = AddOrder(s); // pickup at (21.0m, 105.8m), vehicleTypeId = 1

        // Seed an online, approved driver within 5km with matching vehicleTypeId = 1
        var driverUser = User.Create("Tài Xế A", "0900000011", "drv@x.vn", "h", UserRole.Driver, emailVerified: true);
        driverUser.SetFcmToken("fcm-drv-nearby");
        s.App.Users.Add(driverUser);

        var profile = DriverProfile.Create(driverUser.Id, "079090000001", "790000000001", "f.jpg", "b.jpg");
        TestIds.Set(profile, 11);
        var vehicle = Vehicle.Create(profile.Id, 1, "51C-123.45", 1000m, "250x160x160", "r.jpg", "i.jpg", "v.jpg");
        TestIds.Set(vehicle, 11);
        profile.Approve(vehicle);
        profile.GoOnline();

        var paidTime = Now.AddMinutes(2);
        profile.UpdateLocation(21.01m, 105.81m, paidTime.AddSeconds(-30)); // ~1.51 km away, fresh GPS
        s.App.DriverProfiles.Add(profile);

        var driverWallet = DriverWallet.Create(profile.Id);
        driverWallet.TopUpDeposit(200_000m);
        s.App.DriverWallets.Add(driverWallet);

        await s.App.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var transaction = Assert.Single(s.App.Payments.Items);
        s.App.Time.Now = paidTime;

        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(transaction.PayosOrderCode!.Value, 180_000m)));

        var realtimeNotif = Assert.Single(s.App.Realtime.Sent);
        Assert.Equal("user", realtimeNotif.Target);
        Assert.Equal(driverUser.Id.ToString(), realtimeNotif.Id);
        Assert.Equal("NotificationReceived", realtimeNotif.EventName);
        var notifPayload = Assert.IsType<Notification>(realtimeNotif.Payload);
        Assert.Equal("NEW_MATCHING_ORDER", notifPayload.Type);
        Assert.Equal(order.Id.ToString(), notifPayload.Data?["orderId"]);

        var pushSent = Assert.Single(s.App.Push.Sent);
        Assert.Equal("fcm-drv-nearby", pushSent.Token);
        Assert.Equal("NEW_MATCHING_ORDER", pushSent.Data?["type"]);
    }

    [Fact]
    public async Task Valid_webhook_for_a_driver_deposit_top_up_credits_the_wallet_DRV21()
    {
        var s = Build();
        var wallet = DriverWallet.Create(driverProfileId: 7);
        s.App.DriverWallets.Add(wallet);
        var transaction = PaymentTransaction.ForDeposit(wallet.Id, 200_000m, payosOrderCode: 1760000000999001, "https://pay.payos.vn/web/1", Now);
        s.App.Payments.Add(transaction);
        var paidTime = Now.AddMinutes(1);
        s.App.Time.Now = paidTime;

        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(1760000000999001, 200_000m)));

        Assert.Equal(PaymentTransactionStatus.Paid, transaction.TransactionStatus);
        Assert.Equal(paidTime, transaction.PaidAt);
        Assert.Equal(200_000m, wallet.DepositBalance);
        var walletTx = Assert.Single(wallet.Transactions);
        Assert.Equal((WalletTransactionType.TopUpDeposit, 200_000m, 200_000m, WalletTransactionStatus.Completed),
            (walletTx.TransactionType, walletTx.Amount, walletTx.BalanceAfter, walletTx.TransactionStatus));
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Duplicate_webhook_is_idempotent_and_does_not_credit_or_save_twice()
    {
        var s = Build();
        var wallet = DriverWallet.Create(driverProfileId: 7);
        s.App.DriverWallets.Add(wallet);
        var transaction = PaymentTransaction.ForDeposit(wallet.Id, 100_000m, payosOrderCode: 1760000000999002, "https://pay.payos.vn/web/2", Now);
        s.App.Payments.Add(transaction);

        var firstTime = Now.AddMinutes(1);
        s.App.Time.Now = firstTime;
        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(1760000000999002, 100_000m)));

        s.App.Time.Now = Now.AddMinutes(5);
        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(1760000000999002, 100_000m))); // duplicate delivery

        Assert.Equal(100_000m, wallet.DepositBalance); // credited only once
        Assert.Single(wallet.Transactions);
        Assert.Equal(firstTime, transaction.PaidAt);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Theory]
    [InlineData("wrong-signature")]
    [InlineData("")]
    public async Task Webhook_with_invalid_or_missing_signature_is_rejected_with_401_Unauthorized(string badSignature)
    {
        var s = Build();
        var order = AddOrder(s);
        await s.App.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var transaction = Assert.Single(s.App.Payments.Items);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(transaction.PayosOrderCode!.Value, 180_000m, badSignature))));

        Assert.Equal(PaymentTransactionStatus.Pending, transaction.TransactionStatus);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Webhook_with_mismatched_amount_is_rejected_with_PAYMENT_AMOUNT_MISMATCH()
    {
        var s = Build();
        var order = AddOrder(s);
        await s.App.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var transaction = Assert.Single(s.App.Payments.Items);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(transaction.PayosOrderCode!.Value, 50_000m))));

        Assert.Equal("PAYMENT_AMOUNT_MISMATCH", error.Code);
        Assert.Equal(PaymentTransactionStatus.Pending, transaction.TransactionStatus);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
    }

    [Fact]
    public async Task Webhook_arriving_after_order_was_cancelled_records_payment_and_creates_full_cancellation_refund()
    {
        var s = Build();
        var order = AddOrder(s);
        await s.App.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var transaction = Assert.Single(s.App.Payments.Items);
        order.CancelByCustomer(s.Customer.Id, "Đổi ý", Now.AddMinutes(1), "CHANGED_PLAN");

        var paidTime = Now.AddMinutes(2);
        s.App.Time.Now = paidTime;
        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(transaction.PayosOrderCode!.Value, 180_000m)));

        Assert.Equal(PaymentTransactionStatus.Paid, transaction.TransactionStatus);
        Assert.Equal(paidTime, transaction.PaidAt);
        Assert.Equal(OrderStatus.Cancelled, order.OrderStatus);
        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.Equal((order.Id, RefundSourceType.Cancellation, RefundStatus.Requested), (refund.OrderId, refund.SourceType, refund.RefundStatus));
        Assert.Equal(180_000m, refund.RequestedAmount);
        Assert.Equal(180_000m, refund.ProposedAmount);
        Assert.Equal("CHANGED_PLAN", refund.ReasonCode);
    }

    [Fact]
    public async Task Customer_is_asked_for_a_bank_account_when_the_payment_arrives_after_the_order_was_cancelled_R01()
    {
        var s = Build();
        var order = AddOrder(s);
        await s.App.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var transaction = Assert.Single(s.App.Payments.Items);
        order.CancelByCustomer(s.Customer.Id, "Đổi ý", Now.AddMinutes(1), "CHANGED_PLAN");
        s.App.Time.Now = Now.AddMinutes(2);

        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(transaction.PayosOrderCode!.Value, 180_000m)));

        var ask = Assert.Single(CustomerNotifications(s), n => n.Type == "REFUND_BANK_INFO_REQUIRED");
        Assert.Equal(order.Id.ToString(), ask.Data!["orderId"]);
    }

    [Fact]
    public async Task Customer_is_asked_for_a_bank_account_when_the_same_order_is_paid_twice_R01()
    {
        var s = Build();
        var order = AddOrder(s);
        await s.App.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var first = Assert.Single(s.App.Payments.Items);
        var second = PaymentTransaction.ForOrderViaPayOs(order.Id, order.TotalFee, 1760000000999222L, "https://pay.payos.vn/web/2", Now);
        s.App.Payments.Add(second);
        s.App.Time.Now = Now.AddMinutes(2);
        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(first.PayosOrderCode!.Value, 180_000m)));
        Assert.DoesNotContain(CustomerNotifications(s), n => n.Type == "REFUND_BANK_INFO_REQUIRED"); // the first payment is not a refund

        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(second.PayosOrderCode!.Value, 180_000m)));

        Assert.Single(s.App.Refunds.Items);
        Assert.Single(CustomerNotifications(s), n => n.Type == "REFUND_BANK_INFO_REQUIRED");
    }

    private static List<Notification> CustomerNotifications(Setup s) =>
        s.App.Realtime.Sent
            .Where(x => x is { Target: "user", EventName: RealtimeEvents.NotificationReceived } && x.Id == s.Customer.Id.ToString())
            .Select(x => (Notification)x.Payload)
            .ToList();

    [Fact]
    public async Task Webhook_for_locally_expired_or_replaced_session_still_marks_transaction_and_open_order_paid()
    {
        var s = Build();
        var order = AddOrder(s);
        await s.App.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var firstTx = s.App.Payments.Items[0];

        // Customer asks for a second code at minute 3 (cancelling firstTx locally), or JOB-PAY-01 expired firstTx,
        // but the customer's bank app had already scanned and transferred money to firstTx!
        s.App.Time.Now = Now.AddMinutes(3);
        await s.App.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var secondTx = s.App.Payments.Items[1];
        Assert.Equal(PaymentTransactionStatus.Cancelled, firstTx.TransactionStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, secondTx.TransactionStatus);

        var paidTime = Now.AddMinutes(4);
        s.App.Time.Now = paidTime;
        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(firstTx.PayosOrderCode!.Value, 180_000m)));

        Assert.Equal(PaymentTransactionStatus.Paid, firstTx.TransactionStatus);
        Assert.Equal(paidTime, firstTx.PaidAt);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(paidTime, order.PaidAt);
        // The second pending session is cancelled both in DB and at PayOS:
        Assert.Equal(PaymentTransactionStatus.Cancelled, secondTx.TransactionStatus);
        Assert.Contains(secondTx.PayosOrderCode!.Value, s.App.Gateway.Cancelled);
    }

    [Fact]
    public async Task PayOS_confirm_webhook_probe_with_order_code_123_is_accepted_as_a_no_op()
    {
        var s = Build();

        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(123L, 3_000m)));

        Assert.Empty(s.App.Payments.Items);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Non_success_webhook_code_marks_pending_transaction_failed_without_marking_order_paid()
    {
        var s = Build();
        var order = AddOrder(s);
        await s.App.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        var transaction = Assert.Single(s.App.Payments.Items);

        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(
            WebhookJson(transaction.PayosOrderCode!.Value, 180_000m, code: "01", success: false)));

        Assert.Equal(PaymentTransactionStatus.Failed, transaction.TransactionStatus);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Equal(2, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Unknown_PayOS_order_code_throws_NotFoundException()
    {
        var s = Build();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.App.Sender.Send(new ProcessPayOsWebhookCommand(WebhookJson(9999999999999999, 180_000m))));
    }

    [Theory]
    [InlineData("""{"signature":"valid-payos-signature"}""")]
    [InlineData("""{"data":{"orderCode":0,"amount":180000},"signature":"valid-payos-signature"}""")]
    [InlineData("""{"data":{"orderCode":123,"amount":0},"signature":"valid-payos-signature"}""")]
    public async Task Malformed_webhook_data_throws_ValidationException(string body)
    {
        var s = Build();

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new ProcessPayOsWebhookCommand(body)));
    }

    [Fact]
    public async Task End_to_end_with_real_PayOsPaymentGateway_HMAC_SHA256_signature()
    {
        var s = Build();
        var order = AddOrder(s);
        const long payosOrderCode = 1760000000123456;
        var transaction = PaymentTransaction.ForOrderViaPayOs(order.Id, 180_000m, payosOrderCode, "https://pay.payos.vn/web/1", Now);
        s.App.Payments.Add(transaction);

        const string checksumKey = "real-checksum-secret";
        var realGateway = new PayOsPaymentGateway(
            new HttpClient(new NoOpHttpHandler()),
            new PayOsOptions { ClientId = "c", ApiKey = "a", ChecksumKey = checksumKey },
            NullLogger<PayOsPaymentGateway>.Instance);

        var handler = new ProcessPayOsWebhookCommandHandler(
            realGateway, s.App.Payments, s.App.Orders, s.App.DriverWallets, s.App.UnitOfWork, s.App.Time);

        const string payloadToSign =
            "accountNumber=0123&amount=180000&counterAccountName=&currency=VND&description=ModMove MM20261020001&orderCode=1760000000123456&reference=FT1";
        var signature = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(checksumKey), Encoding.UTF8.GetBytes(payloadToSign))).ToLowerInvariant();

        var rawBody = $$"""
            {
              "code": "00",
              "desc": "success",
              "success": true,
              "data": {
                "orderCode": 1760000000123456,
                "amount": 180000,
                "description": "ModMove MM20261020001",
                "accountNumber": "0123",
                "reference": "FT1",
                "counterAccountName": null,
                "currency": "VND"
              },
              "signature": "{{signature}}"
            }
            """;

        await handler.Handle(new ProcessPayOsWebhookCommand(rawBody), CancellationToken.None);

        Assert.Equal(PaymentTransactionStatus.Paid, transaction.TransactionStatus);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    private sealed class NoOpHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}

