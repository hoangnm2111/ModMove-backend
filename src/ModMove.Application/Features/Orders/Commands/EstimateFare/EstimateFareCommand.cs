using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Commands.EstimateFare;

/// <summary>
/// UC-GST fare estimate, no sign-in needed. A command that saves nothing: the vehicle-fit and coupon rules live in the
/// VehicleType and Coupon entities, which only repositories load (query handlers read via Dapper only).
/// The size is the largest item; the weight is the whole order (DECISIONS 4.2).
/// </summary>
public sealed record EstimateFareCommand(
    long VehicleTypeId, string PickupAddress, string DropoffAddress,
    decimal WeightKg, int LengthCm, int WidthCm, int HeightCm,
    bool HasLoadingService, string? CouponCode) : ICommand<FareEstimateResponse>;

public sealed record FareEstimateResponse(
    decimal DistanceKm, int DurationMinutes,
    decimal DistanceFee, decimal ServiceFee, decimal DiscountAmount, decimal TotalFee);
