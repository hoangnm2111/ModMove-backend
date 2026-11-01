using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Incidents.Queries.GetIncidentById;

/// <summary>
/// UC-DIS-04: view the full details of an in-transit incident including order, cargo, customer, driver,
/// status histories, and refund claim (if any).
/// </summary>
public sealed record GetIncidentByIdQuery(long Id) : IQuery<IncidentDetailResponse>;

public sealed record IncidentDetailResponse(
    long Id,
    long OrderId,
    string OrderCode,
    string OrderStatus,
    string IncidentType,
    string Description,
    IReadOnlyList<string> PhotoUrls,
    decimal? Latitude,
    decimal? Longitude,
    string ResolutionStatus,
    string? ResolutionDecision,
    string? ResolutionNote,
    long ReportedById,
    string ReporterName,
    string ReporterRole,
    long? HandledById,
    string? HandledByName,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt,
    IncidentOrderDetails Order,
    IncidentCustomerDetails Customer,
    IncidentDriverDetails? Driver,
    IncidentProofEvidence? Proof,
    IReadOnlyList<IncidentOrderStatusHistoryItem> StatusHistories,
    IncidentRefundDetails? Refund);

public sealed record IncidentOrderDetails(
    long Id,
    string OrderCode,
    string OrderStatus,
    string PickupAddress,
    string DropoffAddress,
    string PaymentMethod,
    string PaymentStatus,
    decimal TotalFee,
    DateTimeOffset? DeliveredAt,
    IncidentCargoDetails? Cargo);

public sealed record IncidentCargoDetails(
    string CargoType,
    int Quantity,
    decimal WeightKg,
    int LengthCm,
    int WidthCm,
    int HeightCm,
    string? SpecialNotes,
    IReadOnlyList<string> PhotoUrls);

public sealed record IncidentCustomerDetails(
    long Id,
    string FullName,
    string PhoneNumber);

public sealed record IncidentDriverDetails(
    long DriverProfileId,
    long UserId,
    string FullName,
    string PhoneNumber,
    string? LicensePlate,
    string? VehicleTypeName);

public sealed record IncidentOrderStatusHistoryItem(
    long Id,
    string? FromStatus,
    string ToStatus,
    string ChangedByRole,
    long? ChangedByUserId,
    string? ChangedByUserName,
    string? Note,
    DateTimeOffset CreatedAt);

/// <summary>Pickup photos of the driver and the delivery photo, to compare with the cargo the customer declared (UC-DIS-13).</summary>
public sealed record IncidentProofEvidence(
    IReadOnlyList<string> PickupPhotoUrls,
    string? DeliveryPhotoUrl,
    DateTimeOffset? DeliveryCompletionTime,
    string ProofStatus);

/// <summary>The refund requested because of the incident, with what the Dispatcher needs to review it (UC-DIS-08, 09, 14).</summary>
public sealed record IncidentRefundDetails(
    long Id,
    string SourceType,
    string Reason,
    IReadOnlyList<string> EvidenceUrls,
    decimal RequestedAmount,
    decimal? ProposedAmount,
    string? ProposalReason,
    long? ProposedById,
    string? InfoRequestNote,
    string? CustomerReply,
    string RefundStatus,
    DateTimeOffset CreatedAt);
