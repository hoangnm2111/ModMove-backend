using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Exceptions;
using ModMove.Domain.Services;

namespace ModMove.Application.Features.Orders;

public sealed record PricingInput(
    long VehicleTypeId, string PickupAddress, string DropoffAddress,
    decimal WeightKg, int LengthCm, int WidthCm, int HeightCm,
    bool HasLoadingService, string? CouponCode);

public sealed record PricedTrip(VehicleType VehicleType, GeoPoint Pickup, GeoPoint Dropoff, RouteInfo Route, Coupon? Coupon, FareQuote Fare);

/// <summary>
/// One price for both the fare estimate and order creation, so the customer pays what was quoted (DECISIONS 4.4):
/// vehicle and fit (GB-10, 4.2) → Google Maps km (3.4) → B1 fare → coupon (GB-21).
/// </summary>
internal sealed class OrderPricing(
    IMapService maps,
    IRepository<VehicleType> vehicleTypes,
    ICouponRepository coupons,
    IPlatformSettings settings,
    TimeProvider clock)
{
    public async Task<PricedTrip> PriceAsync(PricingInput input, CancellationToken cancellationToken)
    {
        var vehicleType = await vehicleTypes.GetByIdAsync(input.VehicleTypeId, cancellationToken);
        if (vehicleType is null || !vehicleType.IsActive)
            throw new NotFoundException(nameof(VehicleType), input.VehicleTypeId);

        if (!vehicleType.CanCarry(input.WeightKg, input.LengthCm, input.WidthCm, input.HeightCm))
            throw new DomainException("The cargo does not fit this vehicle type.", "VEHICLE_TOO_SMALL");

        Coupon? coupon = null;
        if (!string.IsNullOrWhiteSpace(input.CouponCode))
        {
            coupon = await coupons.GetByCodeAsync(input.CouponCode.Trim().ToUpperInvariant(), cancellationToken)
                ?? throw new DomainException("The coupon code does not exist.", "COUPON_NOT_FOUND");
        }

        var pickup = await maps.GeocodeAsync(input.PickupAddress.Trim(), cancellationToken);
        var dropoff = await maps.GeocodeAsync(input.DropoffAddress.Trim(), cancellationToken);
        var route = await maps.GetRouteAsync(pickup, dropoff, cancellationToken);

        decimal? loadingFee = input.HasLoadingService
            ? await settings.GetDecimalAsync(SettingKeys.LoadingFee, cancellationToken)
            : null;

        var fare = FareCalculator.Calculate(vehicleType, route.DistanceKm, loadingFee, coupon, clock.GetUtcNow());
        return new PricedTrip(vehicleType, pickup, dropoff, route, coupon, fare);
    }
}
