using ModMove.Domain.Entities;

namespace ModMove.UnitTests.Domain;

public class VehicleTypeTests
{
    // Cargo bed 170 x 120 x 120 cm, 500 kg (seeded "Xe van 500kg").
    private static VehicleType Van() => VehicleType.Create("Xe van 500kg", 500, 170, 120, 120, 50_000, 10_000, 80_000);

    [Fact]
    public void Largest_item_that_fits_once_turned_is_accepted_4_2()
    {
        // 100 x 160 x 110 does not fit as entered (160 > 120 wide) but fits lying along the bed.
        Assert.True(Van().CanCarry(weightKg: 100, lengthCm: 100, widthCm: 160, heightCm: 110));
    }

    [Fact]
    public void Item_too_long_for_the_bed_in_any_direction_is_rejected_GB10()
    {
        Assert.False(Van().CanCarry(weightKg: 100, lengthCm: 50, widthCm: 180, heightCm: 50));
    }

    [Fact]
    public void Total_weight_over_the_load_is_rejected_GB10()
    {
        Assert.False(Van().CanCarry(weightKg: 501, lengthCm: 10, widthCm: 10, heightCm: 10));
    }
}
