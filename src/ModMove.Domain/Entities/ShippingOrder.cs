using ModMove.Domain.Common;
using ModMove.Domain.Constants;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>A delivery order from booking to settlement (aggregate root).</summary>
public sealed class ShippingOrder : BaseAuditableEntity, IAggregateRoot
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Accepted, OrderStatus.Cancelled],
        [OrderStatus.Accepted] = [OrderStatus.Arrived, OrderStatus.Cancelled],
        [OrderStatus.Arrived] = [OrderStatus.PickedUp, OrderStatus.Cancelled],
        [OrderStatus.PickedUp] = [OrderStatus.InTransit, OrderStatus.Cancelled],
        [OrderStatus.InTransit] = [OrderStatus.Delivered, OrderStatus.Cancelled],
        [OrderStatus.Delivered] = [OrderStatus.Completed],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = [],
    };

    private readonly List<OrderStatusHistory> _statusHistories = [];

    private ShippingOrder() { }

    public string OrderCode { get; private set; } = null!;
    public long CustomerId { get; private set; }
    public long? DriverProfileId { get; private set; }
    public long VehicleTypeId { get; private set; }
    public long? CouponId { get; private set; }

    public string PickupAddress { get; private set; } = null!;
    public decimal PickupLatitude { get; private set; }
    public decimal PickupLongitude { get; private set; }
    public string DropoffAddress { get; private set; } = null!;
    public decimal DropoffLatitude { get; private set; }
    public decimal DropoffLongitude { get; private set; }
    public string ReceiverName { get; private set; } = null!;
    public string ReceiverPhone { get; private set; } = null!;

    public decimal EstimatedDistanceKm { get; private set; }
    public int EstimatedDurationMin { get; private set; }
    public bool HasLoadingService { get; private set; }
    public decimal DistanceFee { get; private set; }
    public decimal ServiceFee { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TotalFee { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }
    public PayerRole? PayerRole { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Unpaid;
    public OrderStatus OrderStatus { get; private set; } = OrderStatus.Pending;
    public string? CancellationReason { get; private set; }

    /// <summary>The CANCELLATION_REASON code the customer picked; null when a Dispatcher or the System cancelled.</summary>
    public string? CancellationReasonCode { get; private set; }

    public DateTimeOffset WarrantyAcceptedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>When the PayOS payment was confirmed; the matching timer of a PayOS order starts here (DECISIONS 3.2).</summary>
    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Rate applied when the order completed (DECISIONS B4); null until then.</summary>
    public decimal? CommissionRate { get; private set; }

    /// <summary>VND taken by the platform, rounded to the dong; null until completion.</summary>
    public decimal? CommissionAmount { get; private set; }

    /// <summary>Optimistic lock: the first driver to accept wins (GB-08).</summary>
    public int RowVersion { get; private set; }

    public CargoDetail Cargo { get; private set; } = null!;
    public ProofOfDelivery? Proof { get; private set; }
    public IReadOnlyCollection<OrderStatusHistory> StatusHistories => _statusHistories;

    public static ShippingOrder Create(
        string orderCode, long customerId, long vehicleTypeId, long? couponId,
        string pickupAddress, decimal pickupLatitude, decimal pickupLongitude,
        string dropoffAddress, decimal dropoffLatitude, decimal dropoffLongitude,
        string receiverName, string receiverPhone,
        decimal estimatedDistanceKm, int estimatedDurationMin, bool hasLoadingService,
        decimal distanceFee, decimal serviceFee, decimal discountAmount,
        PaymentMethod paymentMethod, PayerRole? payerRole, CargoDetail cargo, DateTimeOffset warrantyAcceptedAt)
    {
        if (paymentMethod == PaymentMethod.Cash && payerRole is null)
            throw new DomainException("A cash order must say who pays: sender or receiver.");

        var order = new ShippingOrder
        {
            OrderCode = orderCode,
            CustomerId = customerId,
            VehicleTypeId = vehicleTypeId,
            CouponId = couponId,
            PickupAddress = pickupAddress,
            PickupLatitude = pickupLatitude,
            PickupLongitude = pickupLongitude,
            DropoffAddress = dropoffAddress,
            DropoffLatitude = dropoffLatitude,
            DropoffLongitude = dropoffLongitude,
            ReceiverName = receiverName,
            ReceiverPhone = receiverPhone,
            EstimatedDistanceKm = estimatedDistanceKm,
            EstimatedDurationMin = estimatedDurationMin,
            HasLoadingService = hasLoadingService,
            DistanceFee = distanceFee,
            ServiceFee = serviceFee,
            DiscountAmount = discountAmount,
            TotalFee = Math.Max(0, distanceFee + serviceFee - discountAmount),
            PaymentMethod = paymentMethod,
            PayerRole = payerRole,
            Cargo = cargo,
            WarrantyAcceptedAt = warrantyAcceptedAt,
        };
        order._statusHistories.Add(OrderStatusHistory.Create(null, OrderStatus.Pending, ChangedByRole.Customer, customerId, null));
        return order;
    }

    public void MarkPaid(DateTimeOffset now)
    {
        if (OrderStatus == OrderStatus.Cancelled)
            throw new DomainException("A cancelled order cannot be paid; refund the payment instead.", "ORDER_CANCELLED");
        PaymentStatus = PaymentStatus.Paid;
        PaidAt ??= now;
        RowVersion++; // a customer cancel racing the webhook must not miss the payment (409 instead)
    }

    /// <summary>GB-08: the first driver to accept is assigned; a stale RowVersion makes the save fail for the others.</summary>
    public void Accept(long driverProfileId, long driverUserId, DateTimeOffset now)
    {
        TransitionTo(OrderStatus.Accepted, ChangedByRole.Driver, driverUserId, null, now);
        DriverProfileId = driverProfileId;
        AcceptedAt = now;
        RowVersion++;
    }

    public void ConfirmArrival(long driverUserId, DateTimeOffset now) =>
        TransitionTo(OrderStatus.Arrived, ChangedByRole.Driver, driverUserId, null, now);

    /// <summary>GB-11: at least 2 photos are needed before the order is PickedUp.</summary>
    public void ConfirmPickup(IEnumerable<string> photoUrls, long driverUserId, DateTimeOffset now)
    {
        var photos = photoUrls.ToList();
        if (photos.Count < BusinessRules.MinPickupPhotos)
            throw new DomainException($"At least {BusinessRules.MinPickupPhotos} pickup photos are required.");

        // The customer rejected the evidence: the driver re-uploads without a status change,
        // at most 5 submissions in all (DECISIONS 6.3); after that the driver reports an incident.
        if (OrderStatus == OrderStatus.PickedUp && Proof?.ProofStatus == ProofStatus.Rejected)
        {
            if (Proof.PickupSubmissionCount >= BusinessRules.MaxPickupSubmissions)
                throw new DomainException(
                    $"Pickup photos were sent {BusinessRules.MaxPickupSubmissions} times. Report an incident instead.",
                    "PICKUP_PHOTO_LIMIT");

            Proof.RecordPickup(photos);
            _statusHistories.Add(OrderStatusHistory.Create(
                OrderStatus.PickedUp, OrderStatus.PickedUp, ChangedByRole.Driver, driverUserId, "Pickup evidence resubmitted"));
            return;
        }

        TransitionTo(OrderStatus.PickedUp, ChangedByRole.Driver, driverUserId, null, now);
        Proof ??= ProofOfDelivery.Create();
        Proof.RecordPickup(photos);
    }

    /// <summary>GB-26: the trip starts only after the customer of the order approves the pickup photos waiting for review.</summary>
    public void ApprovePickupEvidence(long customerId, DateTimeOffset now)
    {
        var proof = EnsurePickupEvidenceToReview(customerId);
        proof.Verify();
        TransitionTo(OrderStatus.InTransit, ChangedByRole.Customer, customerId, null, now);
    }

    /// <summary>DECISIONS 6.3: the driver then sends new photos (at most 5 submissions in all).</summary>
    public void RejectPickupEvidence(string reason, long customerId)
    {
        var proof = EnsurePickupEvidenceToReview(customerId);
        proof.Reject(reason);
        _statusHistories.Add(OrderStatusHistory.Create(OrderStatus, OrderStatus, ChangedByRole.Customer, customerId, reason));
    }

    private ProofOfDelivery EnsurePickupEvidenceToReview(long customerId)
    {
        EnsureCustomer(customerId);
        if (OrderStatus != OrderStatus.PickedUp || Proof is not { ProofStatus: ProofStatus.Pending })
            throw new DomainException("There are no pickup photos waiting for review.");
        return Proof;
    }

    /// <summary>DECISIONS C10: refuses a customer other than the one who placed the order (ORDER_NOT_YOURS).</summary>
    public void EnsureCustomer(long customerId)
    {
        if (customerId != CustomerId)
            throw new DomainException("This order belongs to another customer.", "ORDER_NOT_YOURS");
    }

    /// <summary>
    /// UC-CUS-19: the customer of the order confirms the cargo arrived, which completes it (GB-14). The driver's
    /// wallet is settled in the same transaction by the caller.
    /// </summary>
    public void ConfirmReceipt(long customerId, decimal commissionRate, DateTimeOffset now)
    {
        EnsureCustomer(customerId);
        Complete(commissionRate, ChangedByRole.Customer, customerId, now);
    }

    /// <summary>
    /// UC-CUS-21, DECISIONS 7.3, B9: a complaint while Delivered and within 24 hours of delivery. The Dispatcher proposes
    /// the amount later. "One open complaint per order" needs the refunds table, so the caller checks it; bumping
    /// RowVersion makes a second complaint saved at the same time fail with 409.
    /// </summary>
    public Refund FileComplaint(
        long customerId, string reason, IEnumerable<string>? evidenceUrls, decimal requestedAmount, DateTimeOffset now,
        string? reasonCode = null)
    {
        EnsureCustomer(customerId);
        if (!CanReceiveComplaint(now))
            throw new DomainException(
                "A complaint is accepted only within 24 hours of delivery, before the order completes.", "COMPLAINT_WINDOW_CLOSED");
        EnsureRefundAmount(requestedAmount);

        RowVersion++;
        return Refund.Request(Id, null, RefundSourceType.Complaint, reason, evidenceUrls, requestedAmount, reasonCode);
    }

    /// <summary>
    /// UC-CUS-17, DECISIONS B9: the customer asks for a refund because of an incident on this order, whatever the
    /// incident's state; only money paid through PayOS can come back. "One open refund per incident" and the order-wide
    /// total (<see cref="EnsureRefundFits"/>) are checked by the caller; RowVersion guards a concurrent one.
    /// </summary>
    public Refund RequestIncidentRefund(
        long customerId, long incidentId, string reason, IEnumerable<string>? evidenceUrls, decimal requestedAmount)
    {
        EnsureCustomer(customerId);
        EnsureRefundAmount(requestedAmount);
        if (PaymentMethod != PaymentMethod.PayOS || PaymentStatus != PaymentStatus.Paid)
            throw new DomainException("An incident refund is only allowed for a paid PayOS order.", "REFUND_NOT_ALLOWED");

        RowVersion++;
        return Refund.Request(Id, incidentId, RefundSourceType.Incident, reason, evidenceUrls, requestedAmount);
    }

    /// <summary>
    /// UC-CUS-15, DECISIONS 2.2: once the driver has arrived the customer cannot cancel but reports an incident for a
    /// Dispatcher (GB-17), while the order is Arrived, PickedUp or InTransit. After delivery a complaint is used (7.3).
    /// </summary>
    public InTransitIncident ReportIncidentByCustomer(
        long customerId, IncidentType type, string description, IEnumerable<string>? photoUrls, decimal? latitude, decimal? longitude)
    {
        EnsureCustomer(customerId);
        if (OrderStatus is not (OrderStatus.Arrived or OrderStatus.PickedUp or OrderStatus.InTransit))
            throw new DomainException(
                "An incident can be reported from the driver's arrival until delivery; after delivery, file a complaint.",
                "INCIDENT_NOT_ALLOWED");

        return InTransitIncident.Report(Id, customerId, type, description, photoUrls, latitude, longitude);
    }

    /// <summary>DECISIONS B9: more than 0 and at most what the order cost; larger compensation is handled outside the system.</summary>
    private void EnsureRefundAmount(decimal requestedAmount)
    {
        if (requestedAmount <= 0 || requestedAmount > TotalFee)
            throw new DomainException(
                $"The amount requested must be more than 0 and at most the order total ({TotalFee:0} VND).", "REFUND_AMOUNT_INVALID");
    }

    /// <summary>
    /// DECISIONS B9: all refunds of an order add up to at most its total. <paramref name="alreadyClaimed"/> comes from the
    /// refunds table (IRefundRepository.GetClaimedTotalForOrderAsync).
    /// </summary>
    public void EnsureRefundFits(decimal requestedAmount, decimal alreadyClaimed)
    {
        if (alreadyClaimed + requestedAmount > TotalFee)
            throw new DomainException(
                $"The refunds of this order cannot exceed its total: at most {Math.Max(0, TotalFee - alreadyClaimed):0} VND can still be requested.",
                "REFUND_TOTAL_EXCEEDED");
    }

    /// <summary>
    /// UC-CUS-25: the customer of the order rates its driver. Completed only, within 1 day (GB-20, 7.5); the
    /// one-rating-per-order rule is the unique index on ratings.order_id.
    /// </summary>
    public Rating Rate(long customerId, int stars, string? comment, DateTimeOffset now)
    {
        EnsureCustomer(customerId);
        var driverProfileId = DriverProfileId
            ?? throw new DomainException("Only a completed trip with a driver can be rated.", "RATING_NOT_ALLOWED");
        return Rating.Create(Id, customerId, driverProfileId, stars, comment, CompletedAt, now);
    }

    /// <summary>GB-12: a delivery photo and timestamp are required for Delivered.</summary>
    public void Deliver(string deliveryPhotoUrl, long driverUserId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(deliveryPhotoUrl))
            throw new DomainException("A proof-of-delivery photo is required.");

        TransitionTo(OrderStatus.Delivered, ChangedByRole.Driver, driverUserId, null, now);
        Proof ??= ProofOfDelivery.Create();
        Proof.RecordDelivery(deliveryPhotoUrl, now);
        DeliveredAt = now;
    }

    /// <summary>
    /// GB-14 / DECISIONS B4, M1, M2: the commission is fixed at completion and never changes afterwards.
    /// The driver bears the coupon: commission = rate × total_fee (already after the discount),
    /// and the driver keeps total_fee − commission.
    /// </summary>
    public void Complete(decimal commissionRate, ChangedByRole by, long? userId, DateTimeOffset now)
    {
        if (commissionRate is < 0 or > 1)
            throw new DomainException("The commission rate must be between 0 and 1.");

        TransitionTo(OrderStatus.Completed, by, userId, null, now);
        CompletedAt = now;
        CommissionRate = commissionRate;
        CommissionAmount = CommissionFor(commissionRate);
        RowVersion++; // the customer and the auto-complete job racing: the later save is a 409, so the wallet is settled once
    }

    /// <summary>
    /// DECISIONS M1/M2: commission = rate × total_fee (after the coupon), rounded to the dong. Used at completion and,
    /// before that, for the GB-16 deposit check when a driver takes a cash order.
    /// </summary>
    public decimal CommissionFor(decimal commissionRate) =>
        Math.Round(commissionRate * TotalFee, 0, MidpointRounding.AwayFromZero);

    /// <summary>What the driver earns for this order (DECISIONS M1): total_fee minus the commission.</summary>
    public decimal DriverEarning => TotalFee - (CommissionAmount ?? 0);

    /// <summary>
    /// Cancellation by a Dispatcher (incident, GB-17) or by the System (jobs). A customer uses
    /// <see cref="CancelByCustomer"/>, which has its own limits.
    /// </summary>
    public void Cancel(string reason, ChangedByRole by, long? userId, DateTimeOffset now)
    {
        TransitionTo(OrderStatus.Cancelled, by, userId, reason, now);
        CancellationReason = reason;
        RowVersion++; // a driver accepting with a stale copy gets a 409 instead of reviving the order
    }

    /// <summary>
    /// DECISIONS B3, 2.1, 2.2: a customer may cancel only while the order is Pending or Accepted, free and with a
    /// full refund of what was paid online. From Arrived on, the goods are with the driver: the customer reports an
    /// incident and a Dispatcher decides (no refund). Returns the amount to refund (0 for unpaid or cash orders).
    /// </summary>
    public decimal CancelByCustomer(long customerId, string reason, DateTimeOffset now, string? reasonCode = null)
    {
        if (customerId != CustomerId)
            throw new DomainException("Only the customer who placed the order can cancel it.", "ORDER_NOT_YOURS");
        if (OrderStatus is not (OrderStatus.Pending or OrderStatus.Accepted))
            throw new DomainException(
                "Only a Pending or Accepted order can be cancelled. Once the driver has arrived, report an incident instead.",
                "ORDER_NOT_CANCELLABLE");

        Cancel(reason, ChangedByRole.Customer, customerId, now);
        CancellationReasonCode = reasonCode;
        return PaymentStatus == PaymentStatus.Paid ? TotalFee : 0;
    }

    /// <summary>
    /// DECISIONS 7.3 / GB-18: a complaint is accepted only while the order is Delivered and within 24 hours
    /// of delivery; after that the order completes on its own and no complaint is taken.
    /// </summary>
    public bool CanReceiveComplaint(DateTimeOffset now) =>
        OrderStatus == OrderStatus.Delivered && DeliveredAt is not null &&
        now - DeliveredAt < BusinessRules.ComplaintWindow;

    /// <summary>
    /// GB-17 / DECISIONS 7.2: dispatcher hands the order to another driver; the order goes back to Accepted, so the new
    /// driver arrives, re-checks the cargo and sends pickup photos again, with a fresh count of 5 submissions.
    /// </summary>
    public void ReassignDriver(long newDriverProfileId, long dispatcherId, string? note)
    {
        if (OrderStatus is not (OrderStatus.Accepted or OrderStatus.Arrived or OrderStatus.PickedUp or OrderStatus.InTransit))
            throw new DomainException("Only an order that already has a driver can be reassigned.");

        var from = OrderStatus;
        DriverProfileId = newDriverProfileId;
        OrderStatus = OrderStatus.Accepted;
        Proof?.ResetPickupSubmissions();
        RowVersion++;
        _statusHistories.Add(OrderStatusHistory.Create(from, OrderStatus.Accepted, ChangedByRole.Dispatcher, dispatcherId, note));
    }

    /// <summary>
    /// GB-23: delivered for 24 hours. An open complaint also holds the order (DECISIONS 7.3), but that needs the refunds
    /// table, so the caller checks IRefundRepository.HasOpenComplaintAsync as well.
    /// </summary>
    public bool CanBeAutoCompleted(DateTimeOffset now) =>
        OrderStatus == OrderStatus.Delivered && DeliveredAt is not null &&
        now - DeliveredAt >= BusinessRules.AutoCompleteAfterDelivery;

    /// <summary>
    /// GB-24 / DECISIONS 3.2: a cash order waits for a driver from creation; a PayOS order only from payment,
    /// because drivers cannot see it before that (B10). An unpaid PayOS order never expires here.
    /// </summary>
    public bool IsMatchingExpired(DateTimeOffset now)
    {
        if (OrderStatus != OrderStatus.Pending)
            return false;

        var start = PaymentMethod == PaymentMethod.PayOS ? PaidAt : CreatedAt;
        return start is not null && now - start >= BusinessRules.MatchingTimeout;
    }

    private void TransitionTo(OrderStatus to, ChangedByRole by, long? userId, string? note, DateTimeOffset _)
    {
        if (!AllowedTransitions[OrderStatus].Contains(to))
            throw new DomainException($"An order cannot go from {OrderStatus} to {to}.");

        _statusHistories.Add(OrderStatusHistory.Create(OrderStatus, to, by, userId, note));
        OrderStatus = to;
    }
}
