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
    public void Quote_without_an_expected_total_is_accepted() =>
        new FareQuote(170_000, 0, 0, 170_000).EnsureMatches(null);

    [Fact]
    public void Quote_matching_the_expected_total_is_accepted() =>
        new FareQuote(170_000, 0, 0, 170_000).EnsureMatches(170_000);

    [Fact]
    public void Quote_different_from_the_expected_total_is_FARE_CHANGED_4_4()
    {
        var error = Assert.Throws<DomainException>(() => new FareQuote(170_000, 0, 0, 170_000).EnsureMatches(160_000));

        Assert.Equal("FARE_CHANGED", error.Code);
    }

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
    public void Odd_km_rounds_up_to_the_thousand() =>
        Assert.Equal(174_000m, FareCalculator.Calculate(Van(), 12.34565m, null, null, Now).DistanceFee); // 50,000 + 123,456.5

    [Fact]
    public void Fare_of_91_100_becomes_92_000() =>
        Assert.Equal(92_000m, FareCalculator.Calculate(Van(), 4.11m, null, null, Now).TotalFee); // 50,000 + 41,100

    [Fact]
    public void Odd_loading_fee_rounds_up_and_odd_coupon_rounds_down_to_the_thousand()
    {
        var coupon = Coupon.Create("ODD", 20_000.4m, 100_000, Now.AddDays(-1), Now.AddYears(1), 1_000);

        var fare = FareCalculator.Calculate(Van(), 12m, 100_000.5m, coupon, Now);

        // 170,000 + 101,000 − 20,000: every part and the total are whole thousands, the same number the client sends back.
        Assert.Equal(new FareQuote(170_000, 101_000, 20_000, 251_000), fare);
    }

    [Fact]
    public void Coupon_below_its_minimum_is_rejected_GB21()
    {
        var error = Assert.Throws<DomainException>(() => FareCalculator.Calculate(Van(), 1m, null, Welcome20k(), Now)); // 80,000 < 100,000

        Assert.Equal("COUPON_MIN_ORDER_NOT_MET", error.Code);
    }
}
