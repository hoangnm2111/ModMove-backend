using ModMove.Domain.Entities;
using ModMove.Domain.Exceptions;
using ModMove.Domain.Services;

namespace ModMove.UnitTests.Domain;

public class FareCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    // Seeded "Xe van 500kg": base 50,000, 10,000 per km, minimum 80,000.
    private static VehicleType Van() => VehicleType.Create("Xe van 500kg", 500, 170, 120, 120, 50_000, 10_000, 80_000);

    // Seeded WELCOME20K: 20,000 off orders from 100,000, 1,000 uses.
    private static Coupon Welcome20k() => Coupon.Create("WELCOME20K", 20_000, 100_000, Now.AddDays(-1), Now.AddYears(1), 1_000);

    [Fact]
    public void Distance_fee_is_base_plus_per_km_B1() =>
        Assert.Equal(new FareQuote(170_000, 0, 0, 170_000), FareCalculator.Calculate(Van(), 12m, null, null, Now));

    [Fact]
    public void Short_trip_pays_the_minimum_fare_B1() =>
        Assert.Equal(80_000m, FareCalculator.Calculate(Van(), 1.5m, null, null, Now).DistanceFee);

    [Fact]
    public void Loading_fee_is_added_when_chosen() =>
        Assert.Equal(270_000m, FareCalculator.Calculate(Van(), 12m, 100_000m, null, Now).TotalFee);

    [Fact]
    public void Coupon_is_taken_off_after_loading_fee_GB21() =>
        Assert.Equal(
            new FareQuote(170_000, 100_000, 20_000, 250_000),
            FareCalculator.Calculate(Van(), 12m, 100_000m, Welcome20k(), Now));

    [Fact]
    public void Odd_km_rounds_to_the_dong() =>
        Assert.Equal(173_457m, FareCalculator.Calculate(Van(), 12.34565m, null, null, Now).DistanceFee); // 50,000 + 123,456.5

    [Fact]
    public void Coupon_below_its_minimum_is_rejected_GB21()
    {
        var error = Assert.Throws<DomainException>(() => FareCalculator.Calculate(Van(), 1m, null, Welcome20k(), Now)); // 80,000 < 100,000

        Assert.Equal("COUPON_MIN_ORDER_NOT_MET", error.Code);
    }
}
