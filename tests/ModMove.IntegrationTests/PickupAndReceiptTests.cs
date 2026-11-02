using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Orders.Commands.ConfirmReceipt;
using ModMove.Application.Features.Orders.Commands.ReviewPickupEvidence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>A customer, another customer and a driver with a wallet; orders walked to the step a test needs.</summary>
internal sealed class TripSetup
{
    public static readonly DateTimeOffset Now = TestApp.Start;

    public TestApp App { get; }
    public User Customer { get; }
    public User Other { get; }
    public User Driver { get; }
    public DriverProfile Profile { get; }
    public DriverWallet Wallet { get; }

    public TripSetup(AccountStatus status = AccountStatus.Active)
    {
        Customer = User.Create("An", "0900000001", "an@x.vn", "h", UserRole.Customer, emailVerified: true);
        Other = User.Create("Bình", "0900000002", "binh@x.vn", "h", UserRole.Customer, emailVerified: true);
        Driver = User.Create("Tài", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        Customer.ChangeStatus(status);
        App = TestApp.SignedInAs(Customer, Other, Driver);
        Profile = DriverProfile.Create(Driver.Id, "001", "B2-001", "front.jpg", "back.jpg");
        App.DriverProfiles.Add(Profile);
        Wallet = DriverWallet.Create(Profile.Id);
        Wallet.TopUpDeposit(300_000);
        App.DriverWallets.Add(Wallet);
        App.Settings.Add(PlatformSetting.Create(SettingKeys.CommissionRate, "0.10", Now));
    }

    /// <summary>Total fee 180,000 unless <paramref name="discount"/> says otherwise.</summary>
    public ShippingOrder PickedUp(long? customerId = null, PaymentMethod method = PaymentMethod.PayOS, decimal discount = 20_000)
    {
        var order = ShippingOrder.Create(
            "MM20261020001", customerId ?? Customer.Id, vehicleTypeId: 1, couponId: null,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            12m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: discount,
            method, method == PaymentMethod.Cash ? PayerRole.Sender : null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);
        if (method == PaymentMethod.PayOS)
            order.MarkPaid(Now);
        order.Accept(Profile.Id, Driver.Id, Now);
        order.ConfirmArrival(Driver.Id, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], Driver.Id, Now);
        App.Orders.Add(order);
        return order;
    }

    public ShippingOrder Delivered(long? customerId = null, PaymentMethod method = PaymentMethod.PayOS, decimal discount = 20_000)
    {
        var order = PickedUp(customerId, method, discount);
        order.ApprovePickupEvidence(order.CustomerId, Now);
        order.Deliver("pod.jpg", Driver.Id, Now);
        return order;
    }

    public Notification SentToDriver()
    {
        var sent = Assert.Single(App.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal(("user", Driver.Id.ToString(), RealtimeEvents.NotificationReceived), (sent.Target, sent.Id, sent.EventName));
        return Assert.IsType<Notification>(sent.Payload);
    }
}

/// <summary>POST api/customer/orders/{id}/pickup-evidence (UC-CUS-12; GB-26, DECISIONS 6.3, C10).</summary>
public class ReviewPickupEvidenceTests
{
    [Fact]
    public async Task Approving_starts_the_trip_and_tells_the_driver_GB26()
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        await s.App.Sender.Send(new ReviewPickupEvidenceCommand(order.Id, Approve: true, Reason: null));

        Assert.Equal(OrderStatus.InTransit, order.OrderStatus);
        Assert.Equal(ProofStatus.Verified, order.Proof!.ProofStatus);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
        Assert.Equal("PICKUP_APPROVED", s.SentToDriver().Type);
    }

    [Fact]
    public async Task Rejecting_keeps_the_order_picked_up_and_asks_the_driver_for_new_photos_6_3()
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        await s.App.Sender.Send(new ReviewPickupEvidenceCommand(order.Id, Approve: false, Reason: " Ảnh mờ "));

        Assert.Equal(OrderStatus.PickedUp, order.OrderStatus);
        Assert.Equal((ProofStatus.Rejected, "Ảnh mờ"), (order.Proof!.ProofStatus, order.Proof.RejectionReason));
        var notification = s.SentToDriver();
        Assert.Equal("PICKUP_REJECTED", notification.Type);
        Assert.Equal("Ảnh mờ", notification.Data!["reason"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task Rejecting_needs_a_reason(string? reason)
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ReviewPickupEvidenceCommand(order.Id, false, reason)));
    }

    [Fact]
    public async Task Reason_is_at_most_500_characters()
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new ReviewPickupEvidenceCommand(order.Id, false, new string('x', 501))));
    }

    [Fact]
    public async Task Another_customers_order_is_refused_C10()
    {
        var s = new TripSetup();
        var order = s.PickedUp(customerId: s.Other.Id);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ReviewPickupEvidenceCommand(order.Id, true, null)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Equal(OrderStatus.PickedUp, order.OrderStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Nothing_to_review_before_the_driver_sends_photos()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ReviewPickupEvidenceCommand(order.Id, true, null)));

        Assert.Equal(0, s.App.UnitOfWork.Saves);
        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        var s = new TripSetup();

        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new ReviewPickupEvidenceCommand(999, true, null)));
    }

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var s = new TripSetup(AccountStatus.Locked);
        var order = s.PickedUp();

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new ReviewPickupEvidenceCommand(order.Id, true, null)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Fact]
    public async Task Signed_out_caller_is_unauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => new TestApp().Sender.Send(new ReviewPickupEvidenceCommand(1, true, null)));
}

/// <summary>POST api/customer/orders/{id}/confirm-receipt (UC-CUS-19; GB-14, GB-15, DECISIONS M1, C10).</summary>
public class ConfirmReceiptTests
{
    [Fact]
    public async Task Open_complaint_holds_the_order_until_it_closes_7_3()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        var complaint = order.FileComplaint(s.Customer.Id, "Hàng vỡ", null, 100_000, TripSetup.Now);
        s.App.Refunds.Add(complaint);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ConfirmReceiptCommand(order.Id)));

        Assert.Equal("COMPLAINT_OPEN", error.Code);
        Assert.Equal(OrderStatus.Delivered, order.OrderStatus);
        Assert.Equal(0m, s.Wallet.EarningsBalance);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Approved_complaint_still_holds_the_order_7_3()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        var complaint = order.FileComplaint(s.Customer.Id, "Hàng vỡ", null, 100_000, TripSetup.Now);
        s.App.Refunds.Add(complaint);
        complaint.Propose(50_000, "Một phần", 1);
        complaint.Approve(1, TripSetup.Now);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ConfirmReceiptCommand(order.Id)));

        Assert.Equal("COMPLAINT_OPEN", error.Code);
    }

    [Fact]
    public async Task Someone_elses_order_says_nothing_about_its_complaint_C10()
    {
        var s = new TripSetup();
        var order = s.Delivered(customerId: s.Other.Id);
        s.App.Refunds.Add(order.FileComplaint(s.Other.Id, "Hàng vỡ", null, 100_000, TripSetup.Now));

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ConfirmReceiptCommand(order.Id)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
    }

    [Theory]
    [InlineData("Withdrawn")]
    [InlineData("Rejected")]
    [InlineData("Processed")]
    [InlineData("Failed")]
    public async Task Order_completes_once_the_complaint_is_closed_7_3(string outcome)
    {
        var s = new TripSetup();
        var order = s.Delivered();
        var complaint = order.FileComplaint(s.Customer.Id, "Hàng vỡ", null, 100_000, TripSetup.Now);
        s.App.Refunds.Add(complaint);
        switch (outcome)
        {
            case "Withdrawn":
                complaint.Withdraw();
                break;
            case "Rejected":
                complaint.RejectRefund(1, TripSetup.Now);
                break;
            default:
                complaint.Propose(50_000, "Một phần", 1);
                complaint.Approve(1, TripSetup.Now);
                if (outcome == "Processed")
                    complaint.MarkProcessed("BANK1", TripSetup.Now);
                else
                    complaint.MarkFailed();
                break;
        }

        await s.App.Sender.Send(new ConfirmReceiptCommand(order.Id));

        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
    }

    [Fact]
    public async Task Driver_wallet_never_blocks_the_customer_GB15()
    {
        var s = new TripSetup();
        var order = s.Delivered(method: PaymentMethod.Cash);
        typeof(DriverWallet).GetProperty("DepositBalance")!.SetValue(s.Wallet, 1_000m);
        typeof(DriverWallet).GetProperty("WalletStatus")!.SetValue(s.Wallet, WalletStatus.Frozen);

        await s.App.Sender.Send(new ConfirmReceiptCommand(order.Id));

        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal(-17_000m, s.Wallet.DepositBalance); // 1,000 − 18,000 commission
    }

    [Fact]
    public async Task PayOS_order_completes_and_the_driver_earns_the_fare_minus_commission_M1()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        await s.App.Sender.Send(new ConfirmReceiptCommand(order.Id));

        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal((0.10m, 18_000m), (order.CommissionRate, order.CommissionAmount));
        Assert.Equal(162_000m, s.Wallet.EarningsBalance);
        Assert.Equal(300_000m, s.Wallet.DepositBalance);
        var earning = s.Wallet.Transactions.Last();
        Assert.Equal((WalletTransactionType.TripEarning, order.Id, 162_000m), (earning.TransactionType, earning.OrderId, earning.Amount));
        Assert.Equal(1, s.App.UnitOfWork.Saves);
        Assert.Equal("ORDER_COMPLETED", s.SentToDriver().Type);
    }

    [Fact]
    public async Task Cash_order_takes_the_commission_from_the_deposit_GB15()
    {
        var s = new TripSetup();
        var order = s.Delivered(method: PaymentMethod.Cash);

        await s.App.Sender.Send(new ConfirmReceiptCommand(order.Id));

        Assert.Equal(282_000m, s.Wallet.DepositBalance);
        Assert.Equal(0m, s.Wallet.EarningsBalance);
        var deduction = s.Wallet.Transactions.Last();
        Assert.Equal((WalletTransactionType.CommissionDeduction, order.Id, 18_000m), (deduction.TransactionType, deduction.OrderId, deduction.Amount));
    }

    [Fact]
    public async Task Signed_out_caller_is_unauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => new TestApp().Sender.Send(new ConfirmReceiptCommand(1)));

    [Fact]
    public async Task Fully_discounted_order_completes_with_nothing_to_settle()
    {
        var s = new TripSetup();
        var order = s.Delivered(discount: 200_000);
        var walletEntries = s.Wallet.Transactions.Count;

        await s.App.Sender.Send(new ConfirmReceiptCommand(order.Id));

        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal(0m, order.CommissionAmount);
        Assert.Equal(walletEntries, s.Wallet.Transactions.Count);
    }

    [Fact]
    public async Task Receipt_cannot_be_confirmed_before_delivery()
    {
        var s = new TripSetup();
        var order = s.PickedUp();

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ConfirmReceiptCommand(order.Id)));

        Assert.Equal(0m, s.Wallet.EarningsBalance);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Confirming_twice_settles_the_wallet_once()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        await s.App.Sender.Send(new ConfirmReceiptCommand(order.Id));

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ConfirmReceiptCommand(order.Id)));

        Assert.Equal(162_000m, s.Wallet.EarningsBalance);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Another_customers_order_is_refused_C10()
    {
        var s = new TripSetup();
        var order = s.Delivered(customerId: s.Other.Id);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ConfirmReceiptCommand(order.Id)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Equal(OrderStatus.Delivered, order.OrderStatus);
        Assert.Equal(0m, s.Wallet.EarningsBalance);
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        var s = new TripSetup();

        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new ConfirmReceiptCommand(999)));
    }

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var s = new TripSetup(AccountStatus.Locked);
        var order = s.Delivered();

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new ConfirmReceiptCommand(order.Id)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
        Assert.Equal(OrderStatus.Delivered, order.OrderStatus);
    }
}
