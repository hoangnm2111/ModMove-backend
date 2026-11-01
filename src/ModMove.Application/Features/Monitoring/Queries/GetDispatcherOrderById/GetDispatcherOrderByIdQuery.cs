using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Monitoring.Queries.GetDispatcherOrderById;

/// <summary>
/// UC-DIS-01 Step 6: full order details for Dispatcher delivery monitoring (timeline, customer, receiver, driver/vehicle,
/// route, cargo + photos, proof of delivery, payments, incidents, refunds).
/// </summary>
public sealed record GetDispatcherOrderByIdQuery(long Id) : IQuery<DispatcherOrderDetailResponse>;

public sealed record DispatcherOrderDetailResponse(
    long Id,
    string OrderCode,
    string OrderStatus,
    long VehicleTypeId,
    string VehicleTypeName,
    string PickupAddress,
    decimal PickupLatitude,
    decimal PickupLongitude,
    string DropoffAddress,
    decimal DropoffLatitude,
    decimal DropoffLongitude,
    decimal EstimatedDistanceKm,
    int EstimatedDurationMin,
    bool HasLoadingService,
    decimal DistanceFee,
    decimal ServiceFee,
    decimal DiscountAmount,
    decimal TotalFee,
    string PaymentMethod,
    string? PayerRole,
    string PaymentStatus,
    string? CancellationReason,
    decimal? CommissionRate,
    decimal? CommissionAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? CompletedAt,
    DispatcherOrderCustomerDetails Customer,
    DispatcherOrderReceiverDetails Receiver,
    DispatcherOrderDriverDetails? Driver,
    DispatcherOrderCargoDetails? Cargo,
    DispatcherOrderProofDetails? Proof,
    IReadOnlyList<DispatcherOrderStatusHistoryItem> StatusHistories,
    IReadOnlyList<DispatcherOrderPaymentItem> Payments,
    IReadOnlyList<DispatcherOrderIncidentItem> Incidents,
    IReadOnlyList<DispatcherOrderRefundItem> Refunds);

public sealed record DispatcherOrderCustomerDetails(
    long Id,
    string FullName,
    string PhoneNumber,
    string Email);

public sealed record DispatcherOrderReceiverDetails(
    string Name,
    string PhoneNumber);

public sealed record DispatcherOrderDriverDetails(
    long DriverProfileId,
    long UserId,
    string FullName,
    string PhoneNumber,
    string? LicensePlate,
    decimal? LastLatitude,
    decimal? LastLongitude,
    DateTimeOffset? LastLocationAt);

public sealed record DispatcherOrderCargoDetails(
    string CargoType,
    int Quantity,
    decimal WeightKg,
    int LengthCm,
    int WidthCm,
    int HeightCm,
    IReadOnlyList<string> PhotoUrls,
    string? SpecialNotes);

public sealed record DispatcherOrderProofDetails(
    IReadOnlyList<string> PickupPhotoUrls,
    string ProofStatus,
    int PickupSubmissionCount,
    string? RejectionReason,
    string? DeliveryPhotoUrl,
    DateTimeOffset? DeliveryCompletionTime);

public sealed record DispatcherOrderStatusHistoryItem(
    long Id,
    string? FromStatus,
    string ToStatus,
    string ChangedByRole,
    long? ChangedByUserId,
    string? ChangedByUserName,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record DispatcherOrderPaymentItem(
    long Id,
    string PaymentMethod,
    string? PayerRole,
    decimal Amount,
    long? PayosOrderCode,
    string TransactionStatus,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt);

public sealed record DispatcherOrderIncidentItem(
    long Id,
    string IncidentType,
    string Description,
    string ResolutionStatus,
    string? ResolutionDecision,
    string? ResolutionNote,
    string ReporterName,
    string? HandledByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record DispatcherOrderRefundItem(
    long Id,
    long? IncidentId,
    string SourceType,
    string Reason,
    decimal RequestedAmount,
    decimal? ProposedAmount,
    string RefundStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? ProcessedAt);

