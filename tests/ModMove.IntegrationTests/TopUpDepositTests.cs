using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Payments.Commands.ProcessPayOsWebhook;
using ModMove.Application.Features.Payments.Commands.TopUpDeposit;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>POST api/driver/wallet/topup (UC-DRV-21; DECISIONS 5.2; GB-13, GB-16).</summary>
public class TopUpDepositTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private sealed record Setup(TestApp App, User Driver, DriverProfile Profile, DriverWallet Wallet);

    private static Setup Build(AccountStatus status = AccountStatus.Active, bool approved = true, bool frozen = false)
    {
        var driver = User.Create("Tài", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        driver.ChangeStatus(status);
        var app = TestApp.SignedInAs(driver);

        var profile = DriverProfile.Create(driver.Id, "front.jpg", "back.jpg");
        app.DriverProfiles.Add(profile);
        if (approved)
        {
            var vehicle = Vehicle.Create(profile.Id, 1, "51C-123.45", 1000m, "250x160x160", "r.jpg", "i.jpg", "v.jpg");
            profile.Approve(vehicle);
        }

        var wallet = DriverWallet.Create(profile.Id);
        if (frozen)
            typeof(DriverWallet).GetProperty("WalletStatus")!.SetValue(wallet, WalletStatus.Frozen);
        app.DriverWallets.Add(wallet);
        app.Settings.Add(PlatformSetting.Create(SettingKeys.RequiredDeposit, "200000", Now));

        return new Setup(app, driver, profile, wallet);
    }

    [Fact]
    public async Task Creates_a_10_minute_PayOS_session_for_driver_deposit_with_short_description_DRV21()
    {
        var s = Build();

        var result = await s.App.Sender.Send(new TopUpDepositCommand(200_000m, PaymentPlatform.App));

        var tx = Assert.Single(s.App.Payments.Items);
        Assert.Equal((s.Wallet.Id, PaymentMethod.PayOS, PaymentTransactionStatus.Pending), (tx.WalletId, tx.PaymentMethod, tx.TransactionStatus));
        Assert.Null(tx.OrderId);
        Assert.Equal(200_000m, tx.Amount);
        Assert.Equal(Now.AddMinutes(10), tx.ExpiresAt);

        var asked = Assert.Single(s.App.Gateway.Created);
        Assert.Equal((200_000m, $"COC{s.Wallet.Id}", PaymentPlatform.App, Now.AddMinutes(10)), (asked.Amount, asked.Description, asked.Platform, asked.ExpiresAt));
        Assert.InRange(asked.Description.Length, 1, 9);

        Assert.Equal(new TopUpDepositResponse(
            tx.Id, $"https://pay.payos.vn/web/{tx.PayosOrderCode}", $"qr-{tx.PayosOrderCode}", 200_000m, Now.AddMinutes(10)), result);
        Assert.Equal(0m, s.Wallet.DepositBalance); // only credited when the signed webhook arrives
    }

    [Fact]
    public async Task Calling_again_cancels_the_previous_pending_deposit_session_in_db_and_at_PayOS()
    {
        var s = Build();
        await s.App.Sender.Send(new TopUpDepositCommand(200_000m));
        var firstTx = s.App.Payments.Items[0];
        s.App.Time.Now = Now.AddMinutes(2);

        await s.App.Sender.Send(new TopUpDepositCommand(250_000m));

        Assert.Equal(2, s.App.Payments.Items.Count);
        Assert.Equal(PaymentTransactionStatus.Cancelled, firstTx.TransactionStatus);
        Assert.Equal([firstTx.PayosOrderCode!.Value], s.App.Gateway.Cancelled);
        Assert.Equal(PaymentTransactionStatus.Pending, s.App.Payments.Items[1].TransactionStatus);
    }

    [Fact]
    public async Task End_to_end_top_up_link_followed_by_webhook_credits_deposit_balance()
    {
        var s = Build();
        var created = await s.App.Sender.Send(new TopUpDepositCommand(250_000m));
        var tx = Assert.Single(s.App.Payments.Items);

        s.App.Time.Now = Now.AddMinutes(1);
        var webhookBody = $$"""
        {
          "code": "00",
          "desc": "success",
          "success": true,
          "data": {
            "orderCode": {{tx.PayosOrderCode!.Value}},
            "amount": 250000,
            "description": "COC{{s.Wallet.Id}}",
            "accountNumber": "0123456789",
            "reference": "FT999",
            "currency": "VND",
            "code": "00"
          },
          "signature": "{{FakePaymentGateway.ValidSignature}}"
        }
        """;

        await s.App.Sender.Send(new ProcessPayOsWebhookCommand(webhookBody));

        Assert.Equal(created.TransactionId, tx.Id);
        Assert.Equal(PaymentTransactionStatus.Paid, tx.TransactionStatus);
        Assert.Equal(250_000m, s.Wallet.DepositBalance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1_000)]
    [InlineData(50_000.5)]
    public async Task Non_positive_or_not_whole_VND_amount_fails_validation_DRV21(double badAmount)
    {
        var s = Build();

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new TopUpDepositCommand((decimal)badAmount)));

        Assert.Empty(s.App.Gateway.Created);
    }

    [Fact]
    public async Task Amount_below_missing_deposit_is_rejected_while_any_positive_amount_is_allowed_above_required_deposit_GB32()
    {
        var s = Build();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new TopUpDepositCommand(199_999m)));
        Assert.Equal("TOP_UP_BELOW_MISSING_DEPOSIT", ex.Code);
        Assert.Empty(s.App.Gateway.Created);

        // Once DepositBalance >= 200,000, any whole VND amount > 0 is allowed
        s.Wallet.CreditTopUp(200_000m);
        var res = await s.App.Sender.Send(new TopUpDepositCommand(10_000m));
        Assert.Equal(10_000m, res.Amount);
    }

    [Fact]
    public async Task Unapproved_driver_cannot_top_up_deposit()
    {
        var s = Build(approved: false);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new TopUpDepositCommand(100_000m)));

        Assert.Equal("DRIVER_NOT_APPROVED", error.Code);
        Assert.Empty(s.App.Gateway.Created);
    }

    [Fact]
    public async Task Frozen_wallet_cannot_top_up_deposit()
    {
        var s = Build(frozen: true);

        await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new TopUpDepositCommand(100_000m)));

        Assert.Empty(s.App.Gateway.Created);
    }

    [Fact]
    public async Task Inactive_driver_account_is_refused_B8()
    {
        var s = Build(status: AccountStatus.Inactive);

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            s.App.Sender.Send(new TopUpDepositCommand(100_000m)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }
}

