using ModMove.Domain.Entities;

namespace ModMove.Domain.Services;

/// <summary>Fare of one order in VND, fixed at booking (DECISIONS 4.4).</summary>
public sealed record FareQuote(decimal DistanceFee, decimal ServiceFee, decimal DiscountAmount, decimal TotalFee);

/// <summary>
/// DECISIONS B1: distance fee = max(minimum fare, base fare + price per km × km); the loading fee is added when chosen,
/// then the coupon comes off. No rush-hour or bulky surcharge (B2). The coupon's minimum is checked against the fare
/// before the discount (loading fee included).
/// </summary>
public static class FareCalculator
{
    /// <param name="loadingFee">LOADING_FEE when the customer wants loading, otherwise null.</param>
    public static FareQuote Calculate(VehicleType vehicleType, decimal distanceKm, decimal? loadingFee, Coupon? coupon, DateTimeOffset now)
    {
        var distanceFee = Math.Round(
            Math.Max(vehicleType.MinimumFare, vehicleType.BaseFare + vehicleType.PricePerKm * distanceKm),
            0, MidpointRounding.AwayFromZero);
        var serviceFee = loadingFee ?? 0;
        var subtotal = distanceFee + serviceFee;

        var discount = 0m;
        if (coupon is not null)
        {
            coupon.EnsureApplicable(subtotal, now);
            discount = coupon.DiscountFor(subtotal);
        }

        return new FareQuote(distanceFee, serviceFee, discount, Math.Max(0, subtotal - discount));
    }
}
