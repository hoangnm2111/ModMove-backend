using ModMove.Domain.Entities;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Services;

/// <summary>Fare of one order in VND, fixed at booking (DECISIONS 4.4).</summary>
public sealed record FareQuote(decimal DistanceFee, decimal ServiceFee, decimal DiscountAmount, decimal TotalFee)
{
    /// <summary>
    /// UC-GST-17 6a: the total the customer saw on the estimate. If prices, the coupon or the route changed since then,
    /// the order is refused so the client shows the new total and asks again. Null = the client did not send one.
    /// </summary>
    public void EnsureMatches(decimal? expectedTotalFee)
    {
        if (expectedTotalFee is { } expected && expected != TotalFee)
            throw new DomainException(
                $"The fare changed from {expected:0} to {TotalFee:0} VND. Check the new total and book again.", "FARE_CHANGED");
    }
}

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
        // Every part is whole VND, so the total is too and a client can send it back exactly (expectedTotalFee).
        var distanceFee = ToDong(Math.Max(vehicleType.MinimumFare, vehicleType.BaseFare + vehicleType.PricePerKm * distanceKm));
        var serviceFee = ToDong(loadingFee ?? 0);
        var subtotal = distanceFee + serviceFee;

        var discount = 0m;
        if (coupon is not null)
        {
            coupon.EnsureApplicable(subtotal, now);
            discount = ToDong(coupon.DiscountFor(subtotal));
        }

        return new FareQuote(distanceFee, serviceFee, discount, Math.Max(0, subtotal - discount));
    }

    private static decimal ToDong(decimal amount) => Math.Round(amount, 0, MidpointRounding.AwayFromZero);
}
