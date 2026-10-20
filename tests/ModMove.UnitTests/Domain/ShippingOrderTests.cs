using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

public class ShippingOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static ShippingOrder NewOrder(PaymentMethod method = PaymentMethod.PayOS, PayerRole? payer = null) =>
        ShippingOrder.Create(
            "MM20261020001", customerId: 1, vehicleTypeId: 1, couponId: null,
            "A", 10.1m, 106.1m, "B", 10.2m, 106.2m, "Receiver", "0900000009",
            12.5m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: 20_000,
            method, payer, CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);

    private static ShippingOrder AcceptedOrder()
    {
        var order = NewOrder();
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
    public void Pending_order_is_expired_after_matching_timeout_GB24()
    {
        var order = NewOrder();
        order.CreatedAt = Now;

        Assert.False(order.IsMatchingExpired(Now.AddMinutes(9)));
        Assert.True(order.IsMatchingExpired(Now.AddMinutes(10)));
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
}
