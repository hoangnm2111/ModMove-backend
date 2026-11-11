using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Queries.GetMyOrderDetail;

/// <summary>CUS-10: one of the signed-in customer's orders. Someone else's order is 404, so its existence is not revealed.</summary>
public sealed record GetMyOrderDetailQuery(long OrderId) : IQuery<OrderDetailResponse>;

/// <summary>
/// <see cref="PendingDriverIncidentId"/> (DECISIONS 7.6): the Open or InProgress incident the driver now holding the order
/// reported; while it is set the trip is on hold and pickup photos cannot be approved. The customer's own incidents never set it.
/// </summary>
public sealed record OrderDetailResponse(
    long Id, string OrderCode, string OrderStatus, string PickupAddress, string DropoffAddress, string VehicleTypeName,
    string ReceiverName, string ReceiverPhone, decimal EstimatedDistanceKm, int EstimatedDurationMin, bool HasLoadingService,
    decimal DistanceFee, decimal ServiceFee, decimal DiscountAmount, decimal TotalFee,
    string PaymentMethod, string? PayerRole, string PaymentStatus, DateTimeOffset CreatedAt,
    CargoResponse Cargo, DriverResponse? Driver, ProofResponse? Proof, IReadOnlyList<StatusHistoryResponse> History,
    string? CouponCode, PaymentResponse? Payment, long? PendingDriverIncidentId = null);

public sealed record CargoResponse(
    string CargoType, int Quantity, decimal WeightKg, int LengthCm, int WidthCm, int HeightCm,
    IReadOnlyList<string> PhotoUrls, string? SpecialNotes, string? CargoTypeName);

/// <summary>
/// Pickup and delivery photos; null until the driver sends pickup photos. ProofStatus Pending = waiting for the
/// customer's review (UC-CUS-11, 12); PickupSubmissionCount counts towards the limit of 5 (DECISIONS 6.3).
/// </summary>
public sealed record ProofResponse(
    IReadOnlyList<string> PickupPhotoUrls, string ProofStatus, int PickupSubmissionCount, string? RejectionReason,
    string? DeliveryPhotoUrl, DateTimeOffset? DeliveredAt);

/// <summary>
/// The latest PayOS payment attempt (UC-CUS-27); null for a cash or cancelled order, or before a payment link exists. CheckoutUrl only while
/// the link can still be paid; IsExpired = Pending but past ExpiresAt, so the client offers a new code (DECISIONS 3.1).
/// </summary>
public sealed record PaymentResponse(
    string TransactionStatus, string? CheckoutUrl, DateTimeOffset? ExpiresAt, DateTimeOffset? PaidAt, bool IsExpired);

public sealed record DriverResponse(string FullName, string PhoneNumber, string? LicensePlate);

public sealed record StatusHistoryResponse(string? FromStatus, string ToStatus, string ChangedByRole, string? Note, DateTimeOffset ChangedAt);
