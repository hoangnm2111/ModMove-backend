using ModMove.Application.Common.Interfaces;
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

/// <summary>
/// The pickup and drop-off points the addresses were matched to and the encoded route (<c>polyline</c>, 5 decimals, null
/// when MapVina gives no shape) let the booking screen draw the trip on the map without another MapVina call.
/// </summary>
public sealed record FareEstimateResponse(
    decimal DistanceKm, int DurationMinutes,
    decimal DistanceFee, decimal ServiceFee, decimal DiscountAmount, decimal TotalFee,
    GeoPoint? Pickup = null, GeoPoint? Dropoff = null, string? Polyline = null);
