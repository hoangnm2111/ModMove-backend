using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

public class ShippingOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static ShippingOrder NewOrder(
        PaymentMethod method = PaymentMethod.PayOS, PayerRole? payer = null, decimal distanceFee = 200_000, decimal serviceFee = 0) =>
        ShippingOrder.Create(
            "MM20261020001", customerId: 1, vehicleTypeId: 1, couponId: null,
            "A", 10.1m, 106.1m, "B", 10.2m, 106.2m, "Receiver", "0900000009",
            12.5m, 30, hasLoadingService: serviceFee > 0, distanceFee, serviceFee, discountAmount: 20_000,
            method, payer, CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);

    private static ShippingOrder AcceptedOrder(decimal distanceFee = 200_000, decimal serviceFee = 0)
    {
        var order = NewOrder(distanceFee: distanceFee, serviceFee: serviceFee);
        order.Accept(driverProfileId: 5, driverUserId: 9, Now);
        return order;
    }

    [Fact]
    public void Create_computes_total_from_fees_and_discount()
    {
        Assert.Equal(180_000m, NewOrder().TotalFee);
    }

    [Fact]
    public void Create_cash_order_without_payer_is_rejected()
    {
        Assert.Throws<DomainException>(() => NewOrder(PaymentMethod.Cash, payer: null));
    }

    [Fact]
    public void Accept_assigns_driver_and_bumps_row_version_GB08()
    {
        var order = AcceptedOrder();

        Assert.Equal(OrderStatus.Accepted, order.OrderStatus);
        Assert.Equal(5, order.DriverProfileId);
        Assert.Equal(1, order.RowVersion);
    }

    [Fact]
    public void Accept_twice_is_rejected_GB08()
    {
        var order = AcceptedOrder();

        Assert.Throws<DomainException>(() => order.Accept(6, 10, Now));
    }

    [Fact]
    public void ConfirmPickup_needs_two_photos_GB11()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);

        Assert.Throws<DomainException>(() => order.ConfirmPickup(["only-one.jpg"], 9, Now));
    }

    [Fact]
    public void Trip_cannot_start_before_customer_approves_evidence_GB26()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);

        Assert.Equal(OrderStatus.PickedUp, order.OrderStatus);
        Assert.Throws<DomainException>(() => order.Deliver("pod.jpg", 9, Now));

        order.ApprovePickupEvidence(customerId: 1, Now);
        Assert.Equal(OrderStatus.InTransit, order.OrderStatus);
    }

    [Fact]
    public void Deliver_requires_photo_GB12()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);
        order.ApprovePickupEvidence(1, Now);

        Assert.Throws<DomainException>(() => order.Deliver(" ", 9, Now));

        order.Deliver("pod.jpg", 9, Now);
        Assert.Equal(OrderStatus.Delivered, order.OrderStatus);
        Assert.Equal(Now, order.DeliveredAt);
    }

    [Fact]
    public void Delivered_order_is_auto_completed_after_24_hours_GB23()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);
        order.ApprovePickupEvidence(1, Now);
        order.Deliver("pod.jpg", 9, Now);

        Assert.False(order.CanBeAutoCompleted(Now.AddHours(23)));
        Assert.True(order.CanBeAutoCompleted(Now.AddHours(24)));
    }

    [Fact]
    public void Completed_order_cannot_be_cancelled()
    {
        var order = AcceptedOrder();
        order.Cancel("changed my mind", ChangedByRole.Customer, 1, Now);

        Assert.Equal(OrderStatus.Cancelled, order.OrderStatus);
        Assert.Throws<DomainException>(() => order.Cancel("again", ChangedByRole.Customer, 1, Now));
    }

    [Fact]
    public void ReassignDriver_returns_order_to_Accepted_with_new_driver_GB17()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);

        order.ReassignDriver(newDriverProfileId: 7, dispatcherId: 3, note: "breakdown");

        Assert.Equal(OrderStatus.Accepted, order.OrderStatus);
        Assert.Equal(7, order.DriverProfileId);
    }


    [Fact]
    public void Driver_can_resubmit_pickup_photos_after_customer_rejects_GB26()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);
        order.RejectPickupEvidence("blurry", customerId: 1);

        order.ConfirmPickup(["3.jpg", "4.jpg"], 9, Now);

        Assert.Equal(OrderStatus.PickedUp, order.OrderStatus);
        Assert.Equal(ProofStatus.Pending, order.Proof!.ProofStatus);
        order.ApprovePickupEvidence(1, Now);
        Assert.Equal(OrderStatus.InTransit, order.OrderStatus);
    }

    private static ShippingOrder DeliveredOrder(decimal distanceFee = 200_000, decimal serviceFee = 0)
    {
        var order = AcceptedOrder(distanceFee, serviceFee);
        order.ConfirmArrival(9, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);
        order.ApprovePickupEvidence(1, Now);
        order.Deliver("pod.jpg", 9, Now);
        return order;
    }

    [Fact]
    public void Completion_fixes_commission_on_the_total_after_the_coupon_M1()
    {
        var order = DeliveredOrder(); // distance 200,000 + service 0 − coupon 20,000 = total 180,000; the driver bears the coupon

        order.Complete(0.10m, ChangedByRole.System, null, Now);

        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal(0.10m, order.CommissionRate);
        Assert.Equal(18_000m, order.CommissionAmount);  // 10% × 180,000 (M2: a multiplication)
        Assert.Equal(162_000m, order.DriverEarning);
    }

    [Fact]
    public void Completion_rejects_a_commission_rate_outside_0_to_1()
    {
        var order = DeliveredOrder();

        Assert.Throws<DomainException>(() => order.Complete(1.5m, ChangedByRole.System, null, Now));
        Assert.Equal(OrderStatus.Delivered, order.OrderStatus);
    }

    [Fact]
    public void Commission_includes_the_loading_fee_and_rounds_half_away_from_zero_M1()
    {
        // (155,545 + 100,000 − 20,000) × 0.10 = 23,554.5 → 23,555 (banker's rounding would give 23,554)
        var order = DeliveredOrder(distanceFee: 155_545, serviceFee: 100_000);

        order.Complete(0.10m, ChangedByRole.System, null, Now);

        Assert.Equal(23_555m, order.CommissionAmount);
    }

    [Fact]
    public void Cash_order_waits_for_a_driver_from_creation_GB24_3_2()
    {
        var order = NewOrder(PaymentMethod.Cash, PayerRole.Sender);
        order.CreatedAt = Now;

        Assert.False(order.IsMatchingExpired(Now.AddMinutes(9)));
        Assert.True(order.IsMatchingExpired(Now.AddMinutes(10)));
    }

    [Fact]
    public void PayOs_order_waits_for_a_driver_from_payment_GB24_3_2()
    {
        var order = NewOrder(PaymentMethod.PayOS);
        order.CreatedAt = Now;

        Assert.False(order.IsMatchingExpired(Now.AddHours(1))); // unpaid: drivers cannot see it yet

        order.MarkPaid(Now.AddMinutes(30));
        Assert.False(order.IsMatchingExpired(Now.AddMinutes(39)));
        Assert.True(order.IsMatchingExpired(Now.AddMinutes(40)));
    }

    [Theory]
    [InlineData(false, 0)]        // nothing paid yet
    [InlineData(true, 180_000)]   // paid online: full refund of total_fee
    public void Customer_cancels_free_while_pending_B3(bool paid, double refund)
    {
        var order = NewOrder();
        if (paid)
            order.MarkPaid(Now);

        var amount = order.CancelByCustomer(customerId: 1, "changed plans", Now);

        Assert.Equal((decimal)refund, amount);
        Assert.Equal(OrderStatus.Cancelled, order.OrderStatus);
    }

    [Fact]
    public void Customer_cancels_free_after_a_driver_accepted_2_2()
    {
        var order = AcceptedOrder();
        order.MarkPaid(Now);

        Assert.Equal(180_000m, order.CancelByCustomer(1, "changed plans", Now.AddHours(2))); // no time window
    }

    [Fact]
    public void Customer_cannot_cancel_once_the_driver_arrived_2_1()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);

        var error = Assert.Throws<DomainException>(() => order.CancelByCustomer(1, "too late", Now));

        Assert.Equal("ORDER_NOT_CANCELLABLE", error.Code);
        Assert.Equal(OrderStatus.Arrived, order.OrderStatus);
    }

    [Fact]
    public void Another_customer_cannot_cancel_the_order()
    {
        var error = Assert.Throws<DomainException>(() => NewOrder().CancelByCustomer(customerId: 2, "not mine", Now));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
    }

    [Fact]
    public void Complaint_only_while_delivered_and_within_24_hours_7_3()
    {
        var order = DeliveredOrder();

        Assert.True(order.CanReceiveComplaint(Now.AddHours(23)));
        Assert.False(order.CanReceiveComplaint(Now.AddHours(24)));

        order.Complete(0.10m, ChangedByRole.System, null, Now.AddHours(1));
        Assert.False(order.CanReceiveComplaint(Now.AddHours(2))); // completed: no more complaints
    }

    [Fact]
    public void Pickup_photos_can_be_sent_at_most_five_times_6_3()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now); // 1st
        for (var i = 2; i <= 5; i++)
        {
            order.RejectPickupEvidence("blurry", customerId: 1);
            order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now); // 2nd..5th
        }

        order.RejectPickupEvidence("still blurry", customerId: 1);
        var error = Assert.Throws<DomainException>(() => order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now));

        Assert.Equal("PICKUP_PHOTO_LIMIT", error.Code);
    }

    [Fact]
    public void A_cancelled_order_cannot_be_marked_paid()
    {
        var order = AcceptedOrder();
        order.CancelByCustomer(1, "changed plans", Now);

        var error = Assert.Throws<DomainException>(() => order.MarkPaid(Now));

        Assert.Equal("ORDER_CANCELLED", error.Code);
        Assert.NotEqual(PaymentStatus.Paid, order.PaymentStatus);
    }
}
