using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Matching.Queries.GetNearbyOrderDetail;

/// <summary>
/// UC-DRV-09, Screen #21 (Job Details): full detail of a candidate <c>Pending</c> order before accepting it,
/// including fare breakdown, cargo dimensions/photos, matching countdown and live eligibility (<see cref="NearbyOrderDetailResponse.CanAccept"/>).
/// </summary>
public sealed record GetNearbyOrderDetailQuery(long OrderId) : IQuery<NearbyOrderDetailResponse>;

public sealed record NearbyOrderDetailResponse(
    long OrderId,
    string OrderCode,
    long VehicleTypeId,
    string VehicleTypeName,
    string PickupAddress,
    decimal PickupLatitude,
    decimal PickupLongitude,
    string DropoffAddress,
    decimal DropoffLatitude,
    decimal DropoffLongitude,
    decimal? DistanceToPickupKm,
    decimal EstimatedDistanceKm,
    int EstimatedDurationMin,
    decimal DistanceFee,
    decimal ServiceFee,
    decimal DiscountAmount,
    decimal TotalFee,
    decimal CommissionAmount,
    decimal DriverEarning,
    string PaymentMethod,
    string? PayerRole,
    bool HasLoadingService,
    string CargoType,
    string? CargoTypeName,
    int CargoQuantity,
    decimal CargoWeightKg,
    int CargoLengthCm,
    int CargoWidthCm,
    int CargoHeightCm,
    IReadOnlyList<string> CargoPhotoUrls,
    string? SpecialNotes,
    DateTimeOffset CreatedAt,
    DateTimeOffset MatchingExpiresAt,
    bool CanAccept,
    string? IneligibleReason);

