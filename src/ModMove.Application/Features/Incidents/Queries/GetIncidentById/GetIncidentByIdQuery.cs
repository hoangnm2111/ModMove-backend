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
    IReadOnlyList<IncidentOrderStatusHistoryItem> StatusHistories,
    IncidentRefundSummary? Refund);

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
    string? SpecialNotes);

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

public sealed record IncidentRefundSummary(
    long Id,
    string SourceType,
    decimal RequestedAmount,
    decimal? ProposedAmount,
    string RefundStatus,
    DateTimeOffset CreatedAt);
