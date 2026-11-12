using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Payments.Commands.CreateOrderPayment;
using ModMove.Application.Features.Payments.Commands.ProcessPayOsWebhook;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>R-01 (K2): the payer account PayOS reports in the webhook is kept on the payment, and a missing one never breaks it.</summary>
public class ProcessPayOsWebhookPayerTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private static (TestApp App, PaymentTransaction Transaction, ShippingOrder Order) Build()
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
        return (app, null!, order);
    }

    private static async Task<(TestApp App, PaymentTransaction Transaction, ShippingOrder Order)> BuildWithSession()
    {
        var (app, _, order) = Build();
        await app.Sender.Send(new CreateOrderPaymentCommand(order.Id, PaymentPlatform.Web));
        return (app, Assert.Single(app.Payments.Items), order);
    }

    /// <summary>Each payer field is written as the JSON text given (so a test can send a number, null, or leave it out).</summary>
    private static string Webhook(long orderCode, string? payerFields, string signature = FakePaymentGateway.ValidSignature, decimal amount = 180_000m) =>
        $$"""
        {
          "code": "00",
          "desc": "success",
          "success": true,
          "data": {
            "orderCode": {{orderCode}},
            "amount": {{amount:0}},
            "description": "MM1",
            "accountNumber": "0123456789",
            "reference": "FT26293",
            "currency": "VND",
            "code": "00"{{(payerFields is null ? "" : "," + payerFields)}}
          },
          "signature": "{{signature}}"
        }
        """;

    [Fact]
    public async Task A_paid_webhook_keeps_the_payer_bank_number_and_name_R01()
    {
        var (app, transaction, order) = await BuildWithSession();

        await app.Sender.Send(new ProcessPayOsWebhookCommand(Webhook(transaction.PayosOrderCode!.Value,
            """
            "counterAccountBankId": "970436",
            "counterAccountName": "NGUYEN VAN A",
            "counterAccountNumber": "0011001234567"
            """)));

        Assert.Equal(PaymentTransactionStatus.Paid, transaction.TransactionStatus);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal("970436", transaction.PayerBankBin);
        Assert.Equal("0011001234567", transaction.PayerAccountNumber);
        Assert.Equal("NGUYEN VAN A", transaction.PayerAccountName);
        Assert.True(transaction.HasPayerAccount);
    }

    [Theory]
    [InlineData(null)]                                                                                                  // keys missing
    [InlineData("\"counterAccountBankId\": \"\", \"counterAccountName\": \"\", \"counterAccountNumber\": \"\"")]         // empty
    [InlineData("\"counterAccountBankId\": \"  \", \"counterAccountName\": \" \", \"counterAccountNumber\": \"   \"")]   // blank
    [InlineData("\"counterAccountBankId\": \"null\", \"counterAccountName\": \"null\", \"counterAccountNumber\": \"null\"")] // the text "null"
    [InlineData("\"counterAccountBankId\": null, \"counterAccountName\": null, \"counterAccountNumber\": null")]         // JSON null
    [InlineData("\"counterAccountBankId\": 970436, \"counterAccountName\": 5, \"counterAccountNumber\": 123456")]        // numbers, not text
    public async Task Missing_or_meaningless_payer_fields_are_stored_as_null_and_the_payment_still_succeeds_R01(string? payerFields)
    {
        var (app, transaction, order) = await BuildWithSession();

        await app.Sender.Send(new ProcessPayOsWebhookCommand(Webhook(transaction.PayosOrderCode!.Value, payerFields)));

        Assert.Equal(PaymentTransactionStatus.Paid, transaction.TransactionStatus);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Null(transaction.PayerBankBin);
        Assert.Null(transaction.PayerAccountNumber);
        Assert.Null(transaction.PayerAccountName);
        Assert.False(transaction.HasPayerAccount);
    }

    [Fact]
    public async Task A_repeated_webhook_does_not_overwrite_the_payer_R01()
    {
        var (app, transaction, _) = await BuildWithSession();
        var code = transaction.PayosOrderCode!.Value;

        await app.Sender.Send(new ProcessPayOsWebhookCommand(Webhook(code,
            "\"counterAccountBankId\": \"970436\", \"counterAccountName\": \"NGUYEN VAN A\", \"counterAccountNumber\": \"0011001234567\"")));
        await app.Sender.Send(new ProcessPayOsWebhookCommand(Webhook(code,
            "\"counterAccountBankId\": \"970415\", \"counterAccountName\": \"TRAN VAN B\", \"counterAccountNumber\": \"9999999999\"")));

        Assert.Equal("970436", transaction.PayerBankBin);
        Assert.Equal("0011001234567", transaction.PayerAccountNumber);
        Assert.Equal("NGUYEN VAN A", transaction.PayerAccountName);
    }

    [Fact]
    public async Task A_webhook_with_a_bad_signature_stores_nothing_R01()
    {
        var (app, transaction, _) = await BuildWithSession();

        await Assert.ThrowsAsync<ModMove.Application.Common.Exceptions.UnauthorizedException>(() =>
            app.Sender.Send(new ProcessPayOsWebhookCommand(Webhook(transaction.PayosOrderCode!.Value,
                "\"counterAccountBankId\": \"970436\", \"counterAccountName\": \"X\", \"counterAccountNumber\": \"0011001234567\"",
                signature: "wrong-signature"))));

        Assert.Equal(PaymentTransactionStatus.Pending, transaction.TransactionStatus);
        Assert.Null(transaction.PayerAccountNumber);
    }

    [Fact]
    public async Task A_late_payment_on_a_cancelled_order_still_keeps_the_payer_for_the_refund_R01()
    {
        var (app, transaction, order) = await BuildWithSession();
        order.Cancel("Khách hủy", ChangedByRole.Customer, userId: null, Now.AddMinutes(1));

        await app.Sender.Send(new ProcessPayOsWebhookCommand(Webhook(transaction.PayosOrderCode!.Value,
            "\"counterAccountBankId\": \"970436\", \"counterAccountName\": \"NGUYEN VAN A\", \"counterAccountNumber\": \"0011001234567\"")));

        Assert.Equal("0011001234567", transaction.PayerAccountNumber);
        Assert.Single(app.Refunds.Items); // the 100% cancellation refund the late payment creates
        var notice = Assert.Single(app.Realtime.Sent,
            x => x.EventName == RealtimeEvents.NotificationReceived
                && x.Payload is ModMove.Application.Common.Interfaces.Notification { Type: "REFUND_BANK_INFO_REQUIRED" });
        var payload = Assert.IsType<ModMove.Application.Common.Interfaces.Notification>(notice.Payload);
        Assert.DoesNotContain("0011001234567", payload.Body);
    }

    [Fact]
    public async Task A_deposit_top_up_webhook_also_records_payer_without_affecting_wallet_credit_R01()
    {
        var driver = User.Create("Tài", "0900000009", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        var app = TestApp.SignedInAs(driver);
        var wallet = DriverWallet.Create(driverProfileId: 7);
        app.DriverWallets.Add(wallet);
        var transaction = PaymentTransaction.ForDeposit(wallet.Id, 200_000m, 1760000000999888L, "https://pay.test/topup", Now);
        app.Payments.Add(transaction);

        await app.Sender.Send(new ProcessPayOsWebhookCommand(Webhook(1760000000999888L,
            """
            "counterAccountBankId": "970415",
            "counterAccountName": "TAI XE",
            "counterAccountNumber": "1122334455"
            """,
            amount: 200_000m)));

        Assert.Equal(200_000m, wallet.DepositBalance);
        Assert.Equal(("970415", "1122334455", "TAI XE"),
            (transaction.PayerBankBin, transaction.PayerAccountNumber, transaction.PayerAccountName));
    }

    [Fact]
    public void A_bank_or_account_number_too_long_for_its_column_is_dropped_not_cut_R01()
    {
        var transaction = PaymentTransaction.ForOrderViaPayOs(1, 180_000, 1234567, "https://pay.test/x", Now);

        // Cut to 19 digits, 40 ones would still look like a valid account and send the refund to someone else.
        transaction.RecordPayer(new string('9', 30), new string('1', 40), new string('A', 150));

        Assert.Null(transaction.PayerBankBin);
        Assert.Null(transaction.PayerAccountNumber);
        Assert.False(transaction.HasPayerAccount); // nothing to suggest
        Assert.Equal(100, transaction.PayerAccountName!.Length); // a name is only shown to be confirmed, so cutting it is safe
    }

    [Fact]
    public void A_bank_and_account_number_that_fit_are_kept_whole_R01()
    {
        var transaction = PaymentTransaction.ForOrderViaPayOs(1, 180_000, 1234567, "https://pay.test/x", Now);

        transaction.RecordPayer(new string('9', 20), new string('1', 19), "NGUYEN VAN A");

        Assert.Equal(new string('9', 20), transaction.PayerBankBin);
        Assert.Equal(new string('1', 19), transaction.PayerAccountNumber);
    }
}
