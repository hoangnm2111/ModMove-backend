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
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

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

    public void MarkPaid() => PaymentStatus = PaymentStatus.Paid;

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

    public void Complete(ChangedByRole by, long? userId, DateTimeOffset now)
    {
        TransitionTo(OrderStatus.Completed, by, userId, null, now);
        CompletedAt = now;
    }

    public void Cancel(string reason, ChangedByRole by, long? userId, DateTimeOffset now)
    {
        TransitionTo(OrderStatus.Cancelled, by, userId, reason, now);
        CancellationReason = reason;
    }

    /// <summary>GB-17: dispatcher hands the order to another driver; the order goes back to Accepted.</summary>
    public void ReassignDriver(long newDriverProfileId, long dispatcherId, string? note)
    {
        if (OrderStatus is not (OrderStatus.Accepted or OrderStatus.Arrived or OrderStatus.PickedUp or OrderStatus.InTransit))
            throw new DomainException("Only an order that already has a driver can be reassigned.");

        var from = OrderStatus;
        DriverProfileId = newDriverProfileId;
        OrderStatus = OrderStatus.Accepted;
        RowVersion++;
        _statusHistories.Add(OrderStatusHistory.Create(from, OrderStatus.Accepted, ChangedByRole.Dispatcher, dispatcherId, note));
    }

    public bool CanBeAutoCompleted(DateTimeOffset now) =>
        OrderStatus == OrderStatus.Delivered && DeliveredAt is not null &&
        now - DeliveredAt >= BusinessRules.AutoCompleteAfterDelivery;

    public bool IsMatchingExpired(DateTimeOffset now) =>
        OrderStatus == OrderStatus.Pending && now - CreatedAt >= BusinessRules.MatchingTimeout;

    private void TransitionTo(OrderStatus to, ChangedByRole by, long? userId, string? note, DateTimeOffset _)
    {
        if (!AllowedTransitions[OrderStatus].Contains(to))
            throw new DomainException($"An order cannot go from {OrderStatus} to {to}.");

        _statusHistories.Add(OrderStatusHistory.Create(OrderStatus, to, by, userId, note));
        OrderStatus = to;
    }
}
