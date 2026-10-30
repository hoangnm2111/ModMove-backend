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

    /// <summary>GB-26: the trip starts only after the customer approves the cargo evidence.</summary>
    public void ApprovePickupEvidence(long customerId, DateTimeOffset now)
    {
        if (OrderStatus != OrderStatus.PickedUp || Proof is null)
            throw new DomainException("There is no pickup evidence to approve.");

        Proof.Verify();
        TransitionTo(OrderStatus.InTransit, ChangedByRole.Customer, customerId, null, now);
    }

    public void RejectPickupEvidence(string reason, long customerId)
    {
        if (OrderStatus != OrderStatus.PickedUp || Proof is null)
            throw new DomainException("There is no pickup evidence to reject.");

        Proof.Reject(reason);
        _statusHistories.Add(OrderStatusHistory.Create(OrderStatus, OrderStatus, ChangedByRole.Customer, customerId, reason));
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
    public decimal CancelByCustomer(long customerId, string reason, DateTimeOffset now)
    {
        if (customerId != CustomerId)
            throw new DomainException("Only the customer who placed the order can cancel it.", "ORDER_NOT_YOURS");
        if (OrderStatus is not (OrderStatus.Pending or OrderStatus.Accepted))
            throw new DomainException(
                "Only a Pending or Accepted order can be cancelled. Once the driver has arrived, report an incident instead.",
                "ORDER_NOT_CANCELLABLE");

        Cancel(reason, ChangedByRole.Customer, customerId, now);
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
