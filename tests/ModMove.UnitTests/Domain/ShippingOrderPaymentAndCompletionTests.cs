using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

/// <summary>
/// Unit tests for Payment Expiration (JOB-PAY-01, GB-13, DECISIONS 3.1)
/// and Order Auto-Complete / Completion logic (JOB-ORDER-01, GB-23, DECISIONS 7.3, M1).
/// </summary>
public class ShippingOrderPaymentAndCompletionTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    private static ShippingOrder CreateOrder(
        PaymentMethod method = PaymentMethod.PayOS,
        PayerRole? payer = null,
        decimal distanceFee = 200_000,
        decimal serviceFee = 50_000,
        decimal discountAmount = 20_000)
    {
        var order = ShippingOrder.Create(
            "MM20261020001",
            customerId: 1,
            vehicleTypeId: 1,
            couponId: null,
            "1 Đinh Tiên Hoàng",
            21.0300m,
            105.8550m,
            "100 Cầu Giấy",
            21.0362m,
            105.7906m,
            "Người nhận",
            "0912345678",
            12m,
            30,
            hasLoadingService: serviceFee > 0,
            distanceFee: distanceFee,
            serviceFee: serviceFee,
            discountAmount: discountAmount,
            paymentMethod: method,
            payerRole: method == PaymentMethod.Cash ? payer ?? PayerRole.Sender : null,
            cargo: CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null),
            warrantyAcceptedAt: BaseTime);

        order.CreatedAt = BaseTime;
        return order;
    }

    // ==========================================
    // 1. IsPaymentExpired (JOB-PAY-01, GB-13, DECISIONS 3.1)
    // ==========================================

    [Fact]
    public void IsPaymentExpired_returns_true_when_PayOS_unpaid_order_exceeds_30_minutes_happy_case()
    {
        var order = CreateOrder(PaymentMethod.PayOS);

        // 35 minutes after creation
        var now = BaseTime.AddMinutes(35);

        Assert.True(order.IsPaymentExpired(now));
    }

    [Fact]
    public void IsPaymentExpired_returns_true_at_exact_30_minutes_boundary_edge_case()
    {
        var order = CreateOrder(PaymentMethod.PayOS);

        // Exactly 30 minutes after creation
        var now = BaseTime.AddMinutes(30);

        Assert.True(order.IsPaymentExpired(now));
    }

    [Fact]
    public void IsPaymentExpired_returns_false_when_under_30_minutes_edge_case()
    {
        var order = CreateOrder(PaymentMethod.PayOS);

        // 29 minutes and 59 seconds after creation
        var now = BaseTime.AddMinutes(30).AddSeconds(-1);

        Assert.False(order.IsPaymentExpired(now));
    }

    [Fact]
    public void IsPaymentExpired_returns_false_for_cash_order_even_after_hours_edge_case()
    {
        var order = CreateOrder(PaymentMethod.Cash, PayerRole.Sender);

        var now = BaseTime.AddHours(2);

        Assert.False(order.IsPaymentExpired(now));
    }

    [Fact]
    public void IsPaymentExpired_returns_false_when_order_already_paid_edge_case()
    {
        var order = CreateOrder(PaymentMethod.PayOS);
        order.MarkPaid(BaseTime.AddMinutes(5));

        var now = BaseTime.AddHours(1);

        Assert.False(order.IsPaymentExpired(now));
    }

    [Fact]
    public void IsPaymentExpired_returns_false_when_order_is_cancelled_edge_case()
    {
        var order = CreateOrder(PaymentMethod.PayOS);
        order.CancelByCustomer(1, "Thay đổi kế hoạch", BaseTime.AddMinutes(10));

        var now = BaseTime.AddHours(1);

        Assert.False(order.IsPaymentExpired(now));
    }

    // ==========================================
    // 2. Complete & Settlement Calculation (JOB-ORDER-01, GB-23, DECISIONS 7.3, M1)
    // ==========================================

    [Fact]
    public void Complete_sets_status_completed_and_calculates_commission_and_driver_earning_happy_case()
    {
        // TotalFee = distanceFee (200,000) + serviceFee (50,000) - discount (20,000) = 230,000
        var order = CreateOrder();
        order.MarkPaid(BaseTime.AddMinutes(2));
        order.Accept(driverProfileId: 5, driverUserId: 10, BaseTime.AddMinutes(5));
        order.ConfirmArrival(10, BaseTime.AddMinutes(15));
        order.ConfirmPickup(["p1.jpg", "p2.jpg"], 10, BaseTime.AddMinutes(20));
        order.ApprovePickupEvidence(1, BaseTime.AddMinutes(25));

        var deliveredAt = BaseTime.AddHours(1);
        order.Deliver("pod.jpg", 10, deliveredAt);

        // Complete 24h later
        var completedAt = deliveredAt.AddHours(24);
        var commissionRate = 0.10m; // 10%

        Assert.True(order.CanBeAutoCompleted(completedAt));
        order.Complete(commissionRate, ChangedByRole.System, null, completedAt);

        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal(completedAt, order.CompletedAt);
        Assert.Equal(commissionRate, order.CommissionRate);
        Assert.Equal(23_000m, order.CommissionAmount);
        Assert.Equal(207_000m, order.DriverEarning); // 230,000 - 23,000
        Assert.Equal(ChangedByRole.System, order.StatusHistories.Last().ChangedByRole);
    }

    [Fact]
    public void CanBeAutoCompleted_returns_false_when_under_24_hours_edge_case()
    {
        var order = CreateOrder();
        order.MarkPaid(BaseTime.AddMinutes(2));
        order.Accept(5, 10, BaseTime.AddMinutes(5));
        order.ConfirmArrival(10, BaseTime.AddMinutes(15));
        order.ConfirmPickup(["p1.jpg", "p2.jpg"], 10, BaseTime.AddMinutes(20));
        order.ApprovePickupEvidence(1, BaseTime.AddMinutes(25));

        var deliveredAt = BaseTime.AddHours(1);
        order.Deliver("pod.jpg", 10, deliveredAt);

        // 23 hours 59 minutes 59 seconds later
        var almost24h = deliveredAt.AddHours(24).AddSeconds(-1);
        Assert.False(order.CanBeAutoCompleted(almost24h));
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Accepted)]
    [InlineData(OrderStatus.Arrived)]
    [InlineData(OrderStatus.PickedUp)]
    [InlineData(OrderStatus.InTransit)]
    public void Complete_throws_DomainException_if_order_is_not_Delivered_edge_case(OrderStatus status)
    {
        var order = CreateOrder();
        typeof(ShippingOrder).GetProperty("OrderStatus")!.SetValue(order, status);

        var ex = Assert.Throws<DomainException>(() =>
            order.Complete(0.10m, ChangedByRole.System, null, BaseTime.AddHours(25)));

        Assert.Contains("cannot go from", ex.Message);
    }

    [Fact]
    public void Complete_throws_DomainException_if_order_is_already_Completed_edge_case()
    {
        var order = CreateOrder();
        order.MarkPaid(BaseTime.AddMinutes(2));
        order.Accept(5, 10, BaseTime.AddMinutes(5));
        order.ConfirmArrival(10, BaseTime.AddMinutes(15));
        order.ConfirmPickup(["p1.jpg", "p2.jpg"], 10, BaseTime.AddMinutes(20));
        order.ApprovePickupEvidence(1, BaseTime.AddMinutes(25));
        order.Deliver("pod.jpg", 10, BaseTime.AddHours(1));

        order.Complete(0.10m, ChangedByRole.Customer, 1L, BaseTime.AddHours(2));

        var ex = Assert.Throws<DomainException>(() =>
            order.Complete(0.10m, ChangedByRole.System, null, BaseTime.AddHours(25)));

        Assert.Contains("cannot go from Completed to Completed", ex.Message);
    }
}
