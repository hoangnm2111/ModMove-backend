using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Queries.GetComplaintById;

/// <summary>
/// UC-DIS-12 & UC-DIS-13: view comprehensive complaint details and cross-check evidence against pickup photos,
/// proof-of-delivery photo, customer ratings, trip status history, and reported in-transit incidents.
/// </summary>
public sealed record GetComplaintByIdQuery(long Id) : IQuery<ComplaintDetailResponse>;

public sealed record ComplaintDetailResponse(
    long Id,
    long OrderId,
    string OrderCode,
    string OrderStatus,
    string Reason,
    IReadOnlyList<string> EvidenceUrls,
    decimal RequestedAmount,
    decimal? ProposedAmount,
    string? ProposalReason,
    long? ProposedById,
    string? ProposedByName,
    string? InfoRequestNote,
    string? CustomerReply,
    string Stage,
    string RefundStatus,
    string? BankReferenceCode,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset CreatedAt,
    ComplaintOrderDetails Order,
    ComplaintCustomerDetails Customer,
    ComplaintDriverDetails? Driver,
    ComplaintVerificationEvidence? VerificationEvidence,
    ComplaintRatingDetails? Rating,
    IReadOnlyList<ComplaintIncidentSummary> Incidents,
    IReadOnlyList<ComplaintOrderStatusHistoryItem> StatusHistories,
    string? ReasonCode = null,
    string? ReasonCodeName = null);

public sealed record ComplaintOrderDetails(
    long Id,
    string OrderCode,
    string OrderStatus,
    string PickupAddress,
    string DropoffAddress,
    string PaymentMethod,
    string PaymentStatus,
    decimal TotalFee,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? CompletedAt,
    ComplaintCargoDetails? Cargo);

public sealed record ComplaintCargoDetails(
    string CargoType,
    int Quantity,
    decimal WeightKg,
    int LengthCm,
    int WidthCm,
    int HeightCm,
    string? SpecialNotes,
    IReadOnlyList<string> PhotoUrls,
    string? CargoTypeName = null);

public sealed record ComplaintCustomerDetails(
    long Id,
    string FullName,
    string PhoneNumber);

public sealed record ComplaintDriverDetails(
    long DriverProfileId,
    long UserId,
    string FullName,
    string PhoneNumber,
    string? LicensePlate,
    string? VehicleTypeName);

public sealed record ComplaintVerificationEvidence(
    IReadOnlyList<string> PickupPhotoUrls,
    string? DeliveryPhotoUrl,
    DateTimeOffset? DeliveryCompletionTime,
    string ProofStatus);

public sealed record ComplaintRatingDetails(
    int RatingStars,
    string? ReviewComment,
    DateTimeOffset CreatedAt);

public sealed record ComplaintIncidentSummary(
    long Id,
    string IncidentType,
    string Description,
    string ResolutionStatus,
    string? ResolutionDecision,
    DateTimeOffset CreatedAt);

public sealed record ComplaintOrderStatusHistoryItem(
    long Id,
    string? FromStatus,
    string ToStatus,
    string ChangedByRole,
    long? ChangedByUserId,
    string? ChangedByUserName,
    string? Note,
    DateTimeOffset CreatedAt);
