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
    public void A_dispatcher_opens_an_incident_on_a_running_order_already_in_their_hands_Q6b()
    {
        var order = AcceptedOrder();

        var incident = order.ReportIncidentByDispatcher(dispatcherId: 40, IncidentType.Other, "Tài xế không nghe máy");

        Assert.Equal((40L, IncidentResolutionStatus.InProgress, 40L), (incident.ReportedBy, incident.ResolutionStatus, incident.HandledBy));
        Assert.Null(incident.Latitude); // the vehicle's point comes from the driver's GPS (7.2)
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void A_dispatcher_can_open_an_incident_only_on_a_running_order_Q6b(OrderStatus status)
    {
        var order = NewOrder();
        if (status == OrderStatus.Cancelled)
            order.Cancel("Hủy", ChangedByRole.System, null, Now);
        else if (status != OrderStatus.Pending)
        {
            order.Accept(5, 9, Now);
            order.ConfirmArrival(9, Now);
            order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);
            order.ApprovePickupEvidence(1, Now);
            order.Deliver("pod.jpg", 9, Now);
            if (status == OrderStatus.Completed)
                order.Complete(0.1m, ChangedByRole.System, null, Now);
        }

        var ex = Assert.Throws<DomainException>(() => order.ReportIncidentByDispatcher(40, IncidentType.Other, "Kiểm tra"));

        Assert.Equal("INCIDENT_NOT_ALLOWED", ex.Code);
    }

    [Fact]
    public void Create_computes_total_from_fees_and_discount()
    {
        Assert.Equal(180_000m, NewOrder().TotalFee);
    }

    [Fact]
    public void Commission_is_rate_times_total_rounded_to_the_dong_M1()
    {
        var order = NewOrder(distanceFee: 200_005); // total 180,005 after the 20,000 coupon

        Assert.Equal(18_001m, order.CommissionFor(0.10m)); // 18,000.5 rounds up
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

    // A save that lost the race must fail with 409, whichever of the two writers saved first.
    [Fact]
    public void Cancelling_bumps_row_version_so_a_late_accept_cannot_revive_the_order()
    {
        var order = NewOrder();

        order.CancelByCustomer(1, "Đổi ý", Now);

        Assert.Equal(1, order.RowVersion);
    }

    [Fact]
    public void Marking_paid_bumps_row_version_so_a_cancel_cannot_miss_the_payment()
    {
        var order = NewOrder();

        order.MarkPaid(Now);

        Assert.Equal(1, order.RowVersion);
    }

    [Fact]
    public void A_cancelled_order_cannot_be_cancelled_again()
    {
        var order = NewOrder();
        order.CancelByCustomer(1, "Đổi ý", Now);

        var error = Assert.Throws<DomainException>(() => order.CancelByCustomer(1, "Lần hai", Now));

        Assert.Equal("ORDER_NOT_CANCELLABLE", error.Code);
        Assert.Equal("Đổi ý", order.CancellationReason);
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
    public void New_driver_after_a_reassignment_gets_all_five_pickup_submissions_7_2()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);
        order.RejectPickupEvidence("blurry", customerId: 1);
        order.ConfirmPickup(["3.jpg", "4.jpg"], 9, Now);

        order.ReassignDriver(newDriverProfileId: 7, dispatcherId: 3, note: "breakdown");
        order.ConfirmArrival(11, Now);
        order.ConfirmPickup(["5.jpg", "6.jpg"], 11, Now);

        Assert.Equal(1, order.Proof!.PickupSubmissionCount);
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

    private static ShippingOrder PickedUpOrder()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);
        return order;
    }

    [Fact]
    public void Only_the_customer_of_the_order_reviews_the_pickup_photos_C10()
    {
        var order = PickedUpOrder();

        var approve = Assert.Throws<DomainException>(() => order.ApprovePickupEvidence(customerId: 2, Now));
        var reject = Assert.Throws<DomainException>(() => order.RejectPickupEvidence("mờ", customerId: 2));

        Assert.Equal(("ORDER_NOT_YOURS", "ORDER_NOT_YOURS"), (approve.Code, reject.Code));
        Assert.Equal(ProofStatus.Pending, order.Proof!.ProofStatus);
    }

    [Fact]
    public void Rejected_photos_cannot_be_approved_until_the_driver_sends_new_ones_6_3()
    {
        var order = PickedUpOrder();
        order.RejectPickupEvidence("mờ", customerId: 1);

        Assert.Throws<DomainException>(() => order.ApprovePickupEvidence(1, Now));
        Assert.Throws<DomainException>(() => order.RejectPickupEvidence("lại", 1));

        order.ConfirmPickup(["3.jpg", "4.jpg"], 9, Now);
        order.ApprovePickupEvidence(1, Now);
        Assert.Equal(OrderStatus.InTransit, order.OrderStatus);
    }

    [Fact]
    public void Customer_confirming_receipt_completes_the_order_with_commission_CUS19()
    {
        var order = DeliveredOrder();
        var version = order.RowVersion;

        order.ConfirmReceipt(customerId: 1, commissionRate: 0.10m, Now.AddHours(1));

        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal(Now.AddHours(1), order.CompletedAt);
        Assert.Equal(18_000m, order.CommissionAmount);
        Assert.Equal(ChangedByRole.Customer, order.StatusHistories.Last().ChangedByRole);
        Assert.Equal(version + 1, order.RowVersion); // the auto-complete job racing the customer gets a 409
    }

    [Fact]
    public void Only_the_customer_of_the_order_confirms_receipt_C10()
    {
        var order = DeliveredOrder();

        var error = Assert.Throws<DomainException>(() => order.ConfirmReceipt(customerId: 2, 0.10m, Now));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Equal(OrderStatus.Delivered, order.OrderStatus);
    }

    [Fact]
    public void Receipt_can_only_be_confirmed_once_delivered()
    {
        var order = PickedUpOrder();

        Assert.Throws<DomainException>(() => order.ConfirmReceipt(1, 0.10m, Now));
    }

    private static ShippingOrder CompletedOrder()
    {
        var order = DeliveredOrder();
        order.Complete(0.10m, ChangedByRole.System, null, Now);
        return order;
    }

    [Fact]
    public void Customer_rates_the_driver_of_a_completed_order_GB20()
    {
        var order = CompletedOrder();

        var rating = order.Rate(customerId: 1, stars: 5, "Tốt", Now.AddHours(2));

        Assert.Equal((order.Id, 1L, 5L, 5), (rating.OrderId, rating.CustomerId, rating.DriverProfileId, rating.RatingStars));
        Assert.Equal("Tốt", rating.ReviewComment);
    }

    [Fact]
    public void Only_the_customer_of_the_order_rates_it_C10()
    {
        var order = CompletedOrder();

        var error = Assert.Throws<DomainException>(() => order.Rate(customerId: 2, 5, null, Now));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
    }

    [Fact]
    public void An_order_that_is_not_completed_cannot_be_rated_GB20()
    {
        var order = DeliveredOrder();

        var error = Assert.Throws<DomainException>(() => order.Rate(1, 5, null, Now));

        Assert.Equal("RATING_NOT_ALLOWED", error.Code);
    }

    [Fact]
    public void Rating_window_is_one_day_after_completion_7_5()
    {
        var order = CompletedOrder();

        var error = Assert.Throws<DomainException>(() => order.Rate(1, 5, null, Now.AddDays(1).AddMinutes(1)));

        Assert.Equal("RATING_WINDOW_CLOSED", error.Code);
    }

    [Fact]
    public void Customer_files_a_complaint_within_24_hours_of_delivery_7_3()
    {
        var order = DeliveredOrder();
        var version = order.RowVersion;

        var refund = order.FileComplaint(1, "Hàng vỡ", ["a.jpg"], 100_000, Now.AddHours(23));

        Assert.Equal((RefundSourceType.Complaint, 100_000m, (long?)null), (refund.SourceType, refund.RequestedAmount, refund.IncidentId));
        Assert.Null(refund.ProposedAmount); // the Dispatcher proposes (B9)
        Assert.Equal(version + 1, order.RowVersion); // two complaints racing: the later save is a 409
    }

    [Fact]
    public void Complaint_keeps_the_reason_code_from_the_list()
    {
        var refund = DeliveredOrder().FileComplaint(1, "Hàng bị hư hỏng", null, 100_000, Now, "DAMAGED_CARGO");

        Assert.Equal(("DAMAGED_CARGO", "Hàng bị hư hỏng"), (refund.ReasonCode, refund.Reason));
    }

    [Fact]
    public void Customer_cancellation_keeps_the_reason_code_from_the_list()
    {
        var order = NewOrder();

        order.CancelByCustomer(1, "Thay đổi kế hoạch", Now, "CHANGED_PLAN");

        Assert.Equal(("CHANGED_PLAN", "Thay đổi kế hoạch"), (order.CancellationReasonCode, order.CancellationReason));
    }

    [Fact]
    public void Complaint_after_24_hours_or_before_delivery_is_refused_7_3()
    {
        var late = Assert.Throws<DomainException>(() => DeliveredOrder().FileComplaint(1, "x", null, 1_000, Now.AddHours(24)));
        var early = Assert.Throws<DomainException>(() => AcceptedOrder().FileComplaint(1, "x", null, 1_000, Now));

        Assert.Equal(("COMPLAINT_WINDOW_CLOSED", "COMPLAINT_WINDOW_CLOSED"), (late.Code, early.Code));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(180_001)]
    public void Refund_request_is_above_zero_and_at_most_the_order_total_B9(int amount)
    {
        var order = DeliveredOrder(); // total 180,000

        var complaint = Assert.Throws<DomainException>(() => order.FileComplaint(1, "x", null, amount, Now));
        var incident = Assert.Throws<DomainException>(() => order.RequestIncidentRefund(1, 3, "x", null, amount));

        Assert.Equal(("REFUND_AMOUNT_INVALID", "REFUND_AMOUNT_INVALID"), (complaint.Code, incident.Code));
    }

    [Fact]
    public void Only_the_customer_of_the_order_asks_for_a_refund_C10()
    {
        var order = DeliveredOrder();

        var complaint = Assert.Throws<DomainException>(() => order.FileComplaint(2, "x", null, 1_000, Now));
        var incident = Assert.Throws<DomainException>(() => order.RequestIncidentRefund(2, 3, "x", null, 1_000));

        Assert.Equal(("ORDER_NOT_YOURS", "ORDER_NOT_YOURS"), (complaint.Code, incident.Code));
    }

    [Fact]
    public void Incident_refund_points_at_the_incident_and_waits_for_the_dispatcher_B9()
    {
        var order = AcceptedOrder();
        order.MarkPaid(Now);
        var version = order.RowVersion;

        var refund = order.RequestIncidentRefund(1, incidentId: 3, "Xe hỏng, trễ 3 giờ", ["a.jpg"], 50_000);

        Assert.Equal((RefundSourceType.Incident, (long?)3, 50_000m), (refund.SourceType, refund.IncidentId, refund.RequestedAmount));
        Assert.Null(refund.ProposedAmount);
        Assert.Equal(version + 1, order.RowVersion);
    }

    [Fact]
    public void Incident_refund_needs_a_paid_PayOS_order_B9()
    {
        var cash = NewOrder(PaymentMethod.Cash, PayerRole.Sender);
        var unpaid = NewOrder();

        var onCash = Assert.Throws<DomainException>(() => cash.RequestIncidentRefund(1, 3, "x", null, 1_000));
        var onUnpaid = Assert.Throws<DomainException>(() => unpaid.RequestIncidentRefund(1, 3, "x", null, 1_000));

        Assert.Equal(("REFUND_NOT_ALLOWED", "REFUND_NOT_ALLOWED"), (onCash.Code, onUnpaid.Code));
    }

    [Theory]
    [InlineData(0, 180_000)]
    [InlineData(100_000, 80_000)]
    public void Refunds_may_add_up_to_the_order_total_B9(int claimed, int requested) =>
        NewOrder().EnsureRefundFits(requested, claimed); // total 180,000

    [Theory]
    [InlineData(100_000, 80_001)]
    [InlineData(180_000, 1)]
    public void Refunds_cannot_add_up_to_more_than_the_order_total_B9(int claimed, int requested)
    {
        var error = Assert.Throws<DomainException>(() => NewOrder().EnsureRefundFits(requested, claimed));

        Assert.Equal("REFUND_TOTAL_EXCEEDED", error.Code);
        Assert.Contains((180_000 - claimed).ToString(), error.Message);
    }

    [Fact]
    public void Customer_reports_an_incident_while_the_driver_has_the_order_2_2()
    {
        var order = PickedUpOrder();

        var incident = order.ReportIncidentByCustomer(1, IncidentType.Other, "Tài xế đi sai đường", ["a.jpg"], 21.0m, 105.8m);

        Assert.Equal((order.Id, 1L, IncidentType.Other), (incident.OrderId, incident.ReportedBy, incident.IncidentType));
        Assert.Equal(IncidentResolutionStatus.Open, incident.ResolutionStatus);
    }

    [Fact]
    public void Customer_reports_incidents_only_from_Arrived_to_InTransit_2_2()
    {
        var pending = Assert.Throws<DomainException>(() => NewOrder().ReportIncidentByCustomer(1, IncidentType.Other, "x", null, null, null));
        var delivered = Assert.Throws<DomainException>(() => DeliveredOrder().ReportIncidentByCustomer(1, IncidentType.Other, "x", null, null, null));

        Assert.Equal(("INCIDENT_NOT_ALLOWED", "INCIDENT_NOT_ALLOWED"), (pending.Code, delivered.Code));
    }

    [Fact]
    public void Only_the_customer_of_the_order_reports_on_it_C10()
    {
        var error = Assert.Throws<DomainException>(() => PickedUpOrder().ReportIncidentByCustomer(2, IncidentType.Other, "x", null, null, null));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
    }

    private static ShippingOrder OrderIn(OrderStatus status)
    {
        switch (status)
        {
            case OrderStatus.Pending: return NewOrder();
            case OrderStatus.Accepted: return AcceptedOrder();
            case OrderStatus.Arrived:
                var arrived = AcceptedOrder();
                arrived.ConfirmArrival(9, Now);
                return arrived;
            case OrderStatus.PickedUp: return PickedUpOrder();
            case OrderStatus.InTransit:
                var moving = PickedUpOrder();
                moving.ApprovePickupEvidence(1, Now);
                return moving;
            case OrderStatus.Delivered: return DeliveredOrder();
            case OrderStatus.Completed: return CompletedOrder();
            default:
                var cancelled = NewOrder();
                cancelled.Cancel("x", ChangedByRole.System, null, Now);
                return cancelled;
        }
    }

    [Theory]
    [InlineData(OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Accepted, false)]
    [InlineData(OrderStatus.Arrived, true)]
    [InlineData(OrderStatus.PickedUp, true)]
    [InlineData(OrderStatus.InTransit, true)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Customer_incident_is_allowed_exactly_from_Arrived_to_InTransit_2_2(OrderStatus status, bool allowed)
    {
        var order = OrderIn(status);
        Assert.Equal(status, order.OrderStatus);

        var error = Record.Exception(() => order.ReportIncidentByCustomer(1, IncidentType.Other, "x", null, null, null));

        if (allowed)
            Assert.Null(error);
        else
            Assert.Equal("INCIDENT_NOT_ALLOWED", Assert.IsType<DomainException>(error).Code);
    }

    // UC-CUS-27, DECISIONS 3.1: who may get a PayOS code, and until when.
    private static ShippingOrder PayOsOrderBookedAtNow(PaymentMethod method = PaymentMethod.PayOS)
    {
        var order = NewOrder(method, method == PaymentMethod.Cash ? PayerRole.Sender : null);
        order.CreatedAt = Now;
        return order;
    }

    [Fact]
    public void An_unpaid_PayOS_order_may_get_a_code_for_its_customer_until_30_minutes_after_booking()
    {
        var order = PayOsOrderBookedAtNow();

        order.EnsureCanStartPayOsPayment(customerId: 1, Now);
        order.EnsureCanStartPayOsPayment(customerId: 1, Now.AddMinutes(29).AddSeconds(59));
        var late = Assert.Throws<DomainException>(() => order.EnsureCanStartPayOsPayment(1, Now.AddMinutes(30)));

        Assert.Null(late.Code);
    }

    [Fact]
    public void A_code_for_another_customers_order_is_ORDER_NOT_YOURS_C10()
    {
        var order = PayOsOrderBookedAtNow();

        var error = Assert.Throws<DomainException>(() => order.EnsureCanStartPayOsPayment(customerId: 2, Now));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
    }

    [Fact]
    public void A_cancelled_order_gets_no_code_ORDER_CANCELLED()
    {
        var order = PayOsOrderBookedAtNow();
        order.Cancel("x", ChangedByRole.Customer, 1, Now);

        var error = Assert.Throws<DomainException>(() => order.EnsureCanStartPayOsPayment(1, Now));

        Assert.Equal("ORDER_CANCELLED", error.Code);
    }

    [Fact]
    public void A_cash_order_and_a_paid_order_get_no_code()
    {
        var cash = PayOsOrderBookedAtNow(PaymentMethod.Cash);
        var paid = PayOsOrderBookedAtNow();
        paid.MarkPaid(Now);

        Assert.Throws<DomainException>(() => cash.EnsureCanStartPayOsPayment(1, Now));
        Assert.Throws<DomainException>(() => paid.EnsureCanStartPayOsPayment(1, Now));
    }

    [Fact]
    public void EnsureDriver_refuses_unassigned_or_different_driver_C10()
    {
        var unassigned = Assert.Throws<DomainException>(() => NewOrder().EnsureDriver(driverProfileId: 5));
        var wrongDriver = Assert.Throws<DomainException>(() => AcceptedOrder().EnsureDriver(driverProfileId: 99));

        Assert.Equal(("ORDER_NOT_YOURS", "ORDER_NOT_YOURS"), (unassigned.Code, wrongDriver.Code));
        AcceptedOrder().EnsureDriver(driverProfileId: 5);
    }

    [Fact]
    public void ConfirmPickup_rejects_more_than_five_photos_GB06()
    {
        var order = AcceptedOrder();
        order.ConfirmArrival(9, Now);

        var error = Assert.Throws<DomainException>(() =>
            order.ConfirmPickup(["1.jpg", "2.jpg", "3.jpg", "4.jpg", "5.jpg", "6.jpg"], 9, Now));

        Assert.Equal("TOO_MANY_PHOTOS", error.Code);
    }

    [Fact]
    public void Sender_cash_collection_is_allowed_only_at_pickup_within_500m_and_marks_order_paid_DRV17()
    {
        var order = NewOrder(PaymentMethod.Cash, PayerRole.Sender);
        order.Accept(5, 9, Now);

        var tooEarly = Assert.Throws<DomainException>(() =>
            order.CollectCash(5, order.PickupLatitude, order.PickupLongitude, Now));
        Assert.Equal("CASH_COLLECTION_NOT_ALLOWED", tooEarly.Code);

        order.ConfirmArrival(9, Now);

        var tooFar = Assert.Throws<DomainException>(() =>
            order.CollectCash(5, order.DropoffLatitude, order.DropoffLongitude, Now.AddMinutes(1)));
        Assert.Equal("COLLECT_CASH_WRONG_LOCATION", tooFar.Code);

        var tx = order.CollectCash(5, order.PickupLatitude, order.PickupLongitude, Now.AddMinutes(3));

        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(Now.AddMinutes(3), order.PaidAt);
        Assert.Equal((PaymentMethod.Cash, (PayerRole?)PayerRole.Sender, 180_000m, PaymentTransactionStatus.Paid),
            (tx.PaymentMethod, tx.PayerRole, tx.Amount, tx.TransactionStatus));

        var duplicate = Assert.Throws<DomainException>(() =>
            order.CollectCash(5, order.PickupLatitude, order.PickupLongitude, Now.AddMinutes(4)));
        Assert.Equal("ORDER_ALREADY_PAID", duplicate.Code);
    }

    [Fact]
    public void Sender_cash_collection_is_blocked_once_in_transit()
    {
        // DECISIONS 5.4: only a zero-fee sender order can still reach InTransit unpaid; any other is stopped at pickup.
        var order = NewOrder(PaymentMethod.Cash, PayerRole.Sender, distanceFee: 20_000);
        order.Accept(5, 9, Now);
        order.ConfirmArrival(9, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);
        order.ApprovePickupEvidence(1, Now);

        var inTransit = Assert.Throws<DomainException>(() =>
            order.CollectCash(5, order.PickupLatitude, order.PickupLongitude, Now.AddMinutes(5)));
        Assert.Equal("CASH_COLLECTION_NOT_ALLOWED", inTransit.Code);
    }

    [Fact]
    public void Receiver_cash_collection_requires_arriving_at_dropoff_within_500m_DRV17()
    {
        var order = NewOrder(PaymentMethod.Cash, PayerRole.Receiver);
        order.Accept(5, 9, Now);
        order.ConfirmArrival(9, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);

        var atPickup = Assert.Throws<DomainException>(() =>
            order.CollectCash(5, order.DropoffLatitude, order.DropoffLongitude, Now));
        Assert.Equal("CASH_COLLECTION_NOT_ALLOWED", atPickup.Code);

        order.ApprovePickupEvidence(1, Now);

        var beforeArriveDropoff = Assert.Throws<DomainException>(() =>
            order.CollectCash(5, order.DropoffLatitude, order.DropoffLongitude, Now.AddMinutes(5)));
        Assert.Equal("CASH_COLLECTION_NOT_ALLOWED", beforeArriveDropoff.Code);

        order.MarkArrivedAtDropoff(5, Now.AddMinutes(9));

        var tooFar = Assert.Throws<DomainException>(() =>
            order.CollectCash(5, order.PickupLatitude, order.PickupLongitude, Now.AddMinutes(9)));
        Assert.Equal("COLLECT_CASH_WRONG_LOCATION", tooFar.Code);

        var tx = order.CollectCash(5, order.DropoffLatitude, order.DropoffLongitude, Now.AddMinutes(10));

        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal((PayerRole.Receiver, 180_000m), (tx.PayerRole, tx.Amount));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Accepted, true)]
    [InlineData(OrderStatus.Arrived, true)]
    [InlineData(OrderStatus.PickedUp, true)]
    [InlineData(OrderStatus.InTransit, true)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Driver_incident_is_allowed_from_Accepted_to_InTransit_6_1(OrderStatus status, bool allowed)
    {
        var order = OrderIn(status);
        if (status is OrderStatus.Pending or OrderStatus.Cancelled)
        {
            var notYours = Assert.Throws<DomainException>(() =>
                order.ReportIncidentByDriver(5, 9, IncidentType.VehicleBreakdown, "Xe hỏng", null, 10.7m, 106.7m));
            Assert.Equal("ORDER_NOT_YOURS", notYours.Code);
            return;
        }

        var wrongDriver = Assert.Throws<DomainException>(() =>
            order.ReportIncidentByDriver(99, 99, IncidentType.VehicleBreakdown, "Xe hỏng", null, null, null));
        Assert.Equal("ORDER_NOT_YOURS", wrongDriver.Code);

        var error = Record.Exception(() =>
            order.ReportIncidentByDriver(5, 9, IncidentType.VehicleBreakdown, "Xe hỏng", ["1.jpg"], 10.7m, 106.7m));

        if (allowed)
            Assert.Null(error);
        else
            Assert.Equal("INCIDENT_NOT_ALLOWED", Assert.IsType<DomainException>(error).Code);
    }

    private static ShippingOrder ArrivedCashOrder(PayerRole payer, decimal distanceFee = 200_000)
    {
        var order = NewOrder(PaymentMethod.Cash, payer, distanceFee);
        order.Accept(driverProfileId: 5, driverUserId: 9, Now);
        order.ConfirmArrival(9, Now);
        return order;
    }

    [Fact]
    public void Sender_cash_order_cannot_be_picked_up_before_cash_is_collected_DRV13()
    {
        var order = ArrivedCashOrder(PayerRole.Sender);

        var error = Assert.Throws<DomainException>(() => order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now));

        Assert.Equal("CASH_NOT_COLLECTED", error.Code);
        Assert.Equal(OrderStatus.Arrived, order.OrderStatus);
    }

    [Fact]
    public void Sender_cash_order_is_picked_up_after_collect_cash_DRV13()
    {
        var order = ArrivedCashOrder(PayerRole.Sender);
        order.CollectCash(5, order.PickupLatitude, order.PickupLongitude, Now);

        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);

        Assert.Equal(OrderStatus.PickedUp, order.OrderStatus);
    }

    [Fact]
    public void Receiver_cash_order_is_picked_up_without_collecting()
    {
        var order = ArrivedCashOrder(PayerRole.Receiver);

        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);

        Assert.Equal(OrderStatus.PickedUp, order.OrderStatus);
    }

    [Fact]
    public void Zero_fee_sender_cash_order_can_be_picked_up_without_collecting()
    {
        var order = ArrivedCashOrder(PayerRole.Sender, distanceFee: 20_000); // the 20 000 discount covers it all
        Assert.Equal(0, order.TotalFee);

        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);

        Assert.Equal(OrderStatus.PickedUp, order.OrderStatus);
    }

    [Fact]
    public void Reassigned_sender_cash_order_already_paid_can_be_picked_up_again_7_2()
    {
        var order = ArrivedCashOrder(PayerRole.Sender);
        order.CollectCash(5, order.PickupLatitude, order.PickupLongitude, Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], 9, Now);
        order.ReassignDriver(newDriverProfileId: 6, dispatcherId: 77, note: null);
        order.ConfirmArrival(10, Now);

        order.ConfirmPickup(["3.jpg", "4.jpg"], 10, Now);

        Assert.Equal(OrderStatus.PickedUp, order.OrderStatus);
    }
}
