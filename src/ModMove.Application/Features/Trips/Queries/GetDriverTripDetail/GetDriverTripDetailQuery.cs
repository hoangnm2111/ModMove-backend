using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Queries.GetDriverTripDetail;

/// <summary>UC-DRV-11, 14, 15, 19: full trip detail for an order assigned to the signed-in driver.</summary>
public sealed record GetDriverTripDetailQuery(long OrderId) : IQuery<DriverTripDetailResponse>;

/// <summary>UC-DRV-11: returns the driver's currently running trip (Accepted..InTransit), or null when idle.</summary>
public sealed record GetActiveDriverTripQuery : IQuery<DriverTripDetailResponse?>;

public sealed record DriverTripDetailResponse(
    long Id,
    string OrderCode,
    string OrderStatus,
    string PickupAddress,
    decimal PickupLatitude,
    decimal PickupLongitude,
    string DropoffAddress,
    decimal DropoffLatitude,
    decimal DropoffLongitude,
    GeoPoint? HandoverPoint,
    string VehicleTypeName,
    decimal EstimatedDistanceKm,
    int EstimatedDurationMin,
    bool HasLoadingService,
    string CustomerName,
    string CustomerPhone,
    string ReceiverName,
    string ReceiverPhone,
    decimal DistanceFee,
    decimal ServiceFee,
    decimal DiscountAmount,
    decimal TotalFee,
    decimal CommissionRate,
    decimal CommissionAmount,
    decimal DriverEarning,
    string PaymentMethod,
    string? PayerRole,
    string PaymentStatus,
    decimal CashToCollect,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? CompletedAt,
    string? CancellationReason,
    DriverTripCargoResponse Cargo,
    DriverTripProofResponse? Proof,
    IReadOnlyList<DriverTripHistoryResponse> StatusHistories,
    IReadOnlyList<DriverTripIncidentResponse> Incidents,
    DriverTripRatingResponse? Rating,
    string? SenderName = null,
    string? SenderPhone = null,
    string? PickupNote = null,
    string? DropoffNote = null);

public sealed record DriverTripCargoResponse(
    string CargoType,
    string? CargoTypeName,
    int Quantity,
    decimal WeightKg,
    int LengthCm,
    int WidthCm,
    int HeightCm,
    IReadOnlyList<string> PhotoUrls,
    string? SpecialNotes);

public sealed record DriverTripProofResponse(
    IReadOnlyList<string> PickupPhotoUrls,
    string ProofStatus,
    int PickupSubmissionCount,
    int RemainingPickupSubmissions,
    string? RejectionReason,
    string? DeliveryPhotoUrl,
    DateTimeOffset? DeliveryCompletionTime);

public sealed record DriverTripHistoryResponse(
    string? FromStatus,
    string ToStatus,
    string ChangedByRole,
    string? Note,
    DateTimeOffset ChangedAt);

public sealed record DriverTripIncidentResponse(
    long Id,
    string IncidentType,
    string Description,
    IReadOnlyList<string> PhotoUrls,
    bool ReportedByYou,
    string ResolutionStatus,
    string? ResolutionDecision,
    string? ResolutionNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record DriverTripRatingResponse(
    int RatingStars,
    string? ReviewComment,
    DateTimeOffset CreatedAt);

