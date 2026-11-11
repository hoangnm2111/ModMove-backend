using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.UnitTests.Application;

/// <summary>
/// Unit tests for Driver Matching notifications, event templates, and notification helpers (DECISIONS 8.3, B17).
/// </summary>
public class MatchingAndNotificationUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private sealed class CapturingNotificationService : INotificationService
    {
        public List<(long UserId, Notification Notification)> Sent { get; } = [];

        public Task NotifyAsync(long userId, Notification notification, CancellationToken cancellationToken = default)
        {
            Sent.Add((userId, notification));
            return Task.CompletedTask;
        }

        public Task NotifyDispatchersAsync(Notification notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishOrderStatusAsync(ShippingOrder order, long? driverUserId = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static ShippingOrder SampleOrder()
    {
        var order = ShippingOrder.Create(
            "MM20261103999",
            1,
            1,
            null,
            "1 Lê Duẩn",
            10.77m,
            106.70m,
            "2 Hai Bà Trưng",
            10.78m,
            106.71m,
            "Người Nhận",
            "0900000000",
            3.5m,
            15,
            false,
            80_000m,
            0m,
            0m,
            PaymentMethod.Cash,
            PayerRole.Sender,
            CargoDetail.Create("PARCEL", 1, 5m, 30, 20, 20, ["p.jpg"], null),
            Now);
        order.CreatedAt = Now;
        return order;
    }

    [Fact]
    public void All_DriverEvent_values_have_valid_templates()
    {
        foreach (DriverEvent evt in Enum.GetValues<DriverEvent>())
        {
            var (type, title, bodyTemplate) = DriverNotifications.GetTemplate(evt);

            Assert.False(string.IsNullOrWhiteSpace(type), $"Type for {evt} must not be empty.");
            Assert.False(string.IsNullOrWhiteSpace(title), $"Title for {evt} must not be empty.");
            Assert.False(string.IsNullOrWhiteSpace(bodyTemplate), $"BodyTemplate for {evt} must not be empty.");
        }
    }

    [Theory]
    [InlineData(CustomerEvent.RefundBankInfoRequired, "REFUND_BANK_INFO_REQUIRED")]
    [InlineData(CustomerEvent.RefundPayoutFailed, "REFUND_PAYOUT_FAILED")]
    public async Task Refund_payout_events_carry_the_order_code_and_never_a_bank_account_R01(CustomerEvent evt, string expectedType)
    {
        var service = new CapturingNotificationService();
        var order = SampleOrder();

        await service.NotifyCustomerAsync(order, evt, CancellationToken.None);

        var (_, notif) = Assert.Single(service.Sent);
        Assert.Equal(expectedType, notif.Type);
        Assert.Contains("MM20261103999", notif.Body);
        Assert.DoesNotMatch(@"\d{6,}", notif.Body.Replace("MM20261103999", ""));
    }

    [Fact]
    public async Task NotifyNewMatchingOrderAsync_formats_distance_with_invariant_culture_and_populates_data()
    {
        var service = new CapturingNotificationService();
        var order = SampleOrder();

        await service.NotifyNewMatchingOrderAsync(driverUserId: 50, order, distanceKm: 2.75m, CancellationToken.None);

        var (userId, notif) = Assert.Single(service.Sent);
        Assert.Equal(50, userId);
        Assert.Equal("NEW_MATCHING_ORDER", notif.Type);
        Assert.Equal("Đơn hàng mới gần bạn", notif.Title);
        Assert.Contains("2.75", notif.Body); // not "2,75"
        Assert.NotNull(notif.Data);
        Assert.Equal(order.Id.ToString(), notif.Data!["orderId"]);
        Assert.Equal("MM20261103999", notif.Data["orderCode"]);
        Assert.Equal("2.75", notif.Data["distanceKm"]);
    }

    [Fact]
    public async Task NotifyApplicationApprovedAsync_and_RejectedAsync_populate_expected_fields()
    {
        var service = new CapturingNotificationService();

        await service.NotifyApplicationApprovedAsync(driverUserId: 77, applicationId: 123, CancellationToken.None);
        await service.NotifyApplicationRejectedAsync(driverUserId: 88, applicationId: 456, CancellationToken.None);

        Assert.Equal(2, service.Sent.Count);

        var approved = service.Sent[0];
        Assert.Equal(77, approved.UserId);
        Assert.Equal("DRIVER_APPLICATION_APPROVED", approved.Notification.Type);
        Assert.Equal("123", approved.Notification.Data?["applicationId"]);

        var rejected = service.Sent[1];
        Assert.Equal(88, rejected.UserId);
        Assert.Equal("DRIVER_APPLICATION_REJECTED", rejected.Notification.Type);
        Assert.Equal("456", rejected.Notification.Data?["applicationId"]);
    }

    [Fact]
    public async Task NotifyWithdrawalApprovedAsync_populates_reference_code_and_formatted_amount()
    {
        var service = new CapturingNotificationService();
        const decimal amount = 500_000m;

        await service.NotifyWithdrawalApprovedAsync(
            driverUserId: 99,
            walletId: 10,
            transactionId: 101,
            amount: amount,
            status: "Completed",
            referenceCode: "VIB12345678",
            CancellationToken.None);

        var (userId, notif) = Assert.Single(service.Sent);
        Assert.Equal(99, userId);
        Assert.Equal("WITHDRAWAL_DECIDED", notif.Type);
        Assert.Contains(amount.ToString("N0"), notif.Body);
        Assert.Contains("VIB12345678", notif.Body);
        Assert.Equal("101", notif.Data?["transactionId"]);
        Assert.Equal("10", notif.Data?["walletId"]);
        Assert.Equal("Completed", notif.Data?["status"]);
        Assert.Equal("VIB12345678", notif.Data?["referenceCode"]);
    }

    [Theory]
    [InlineData("Sai số tài khoản ngân hàng", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public async Task NotifyWithdrawalRejectedAsync_handles_optional_reason(string? reason, bool shouldContainReasonText)
    {
        var service = new CapturingNotificationService();
        const decimal amount = 250_000m;

        await service.NotifyWithdrawalRejectedAsync(
            driverUserId: 99,
            walletId: 10,
            transactionId: 102,
            amount: amount,
            status: "Rejected",
            reason: reason,
            CancellationToken.None);

        var (userId, notif) = Assert.Single(service.Sent);
        Assert.Equal(99, userId);
        Assert.Equal("WITHDRAWAL_DECIDED", notif.Type);
        Assert.Contains(amount.ToString("N0"), notif.Body);

        if (shouldContainReasonText)
        {
            Assert.Contains("Lý do: Sai số tài khoản ngân hàng", notif.Body);
        }
        else
        {
            Assert.DoesNotContain("Lý do:", notif.Body);
        }
    }
}
