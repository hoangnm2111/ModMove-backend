using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Matching.Queries.GetNearbyOrders;

/// <summary>
/// UC-DRV-08, Screen 2.1.1 (Dispatch Job Alert): Pending orders within <c>MATCHING_RADIUS_KM</c> (5 km) of the signed-in
/// driver's last GPS point that the driver is eligible to take right now (B5, GB-08..10, GB-14, GB-16, GB-24, GB-31, 6.4),
/// ordered nearest pickup first. Returns an empty list when the driver is Offline, unapproved, has a stale GPS point
/// (&gt;= 5 minutes old) or already has an active order.
/// </summary>
public sealed record GetNearbyOrdersQuery : IQuery<IReadOnlyList<NearbyOrderDto>>;

public sealed record NearbyOrderDto(
    long OrderId,
    string OrderCode,
    string PickupAddress,
    decimal PickupLatitude,
    decimal PickupLongitude,
    string DropoffAddress,
    decimal DropoffLatitude,
    decimal DropoffLongitude,
    decimal DistanceToPickupKm,
    decimal EstimatedDistanceKm,
    int EstimatedDurationMin,
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
    DateTimeOffset CreatedAt);

