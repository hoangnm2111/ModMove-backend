using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

/// <summary>DECISIONS B19 (accuracy over 100 m dropped), B20 (mock location refused), B21 (an older point never overwrites a newer one).</summary>
public class DriverProfileLocationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private static DriverProfile Located()
    {
        var profile = DriverProfile.Create(1, "001", "B2-001", "front.jpg", "back.jpg");
        profile.UpdateLocation(21.00m, 105.80m, Now);
        return profile;
    }

    private static void AssertUnchanged(DriverProfile profile) =>
        Assert.Equal((21.00m, 105.80m, Now), (profile.LastLatitude!.Value, profile.LastLongitude!.Value, profile.LastLocationAt!.Value));

    [Fact]
    public void Mock_location_is_refused_B20()
    {
        var profile = Located();

        var error = Assert.Throws<DomainException>(() => profile.UpdateLocation(21.03m, 105.85m, Now.AddSeconds(5), isMocked: true));

        Assert.Equal("MOCK_LOCATION", error.Code);
        AssertUnchanged(profile);
    }

    [Fact]
    public void Accuracy_over_100_m_is_dropped_B19()
    {
        var profile = Located();

        Assert.Equal(LocationUpdateResult.Inaccurate, profile.UpdateLocation(21.03m, 105.85m, Now.AddSeconds(5), accuracyMeters: 100.1m));
        AssertUnchanged(profile);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(null)]
    public void Accuracy_up_to_100_m_or_unknown_is_applied_B19(int? accuracy)
    {
        var profile = Located();

        Assert.Equal(LocationUpdateResult.Applied, profile.UpdateLocation(21.03m, 105.85m, Now.AddSeconds(5), accuracyMeters: accuracy));
        Assert.Equal((21.03m, 105.85m, Now.AddSeconds(5)), (profile.LastLatitude!.Value, profile.LastLongitude!.Value, profile.LastLocationAt!.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void A_point_not_newer_than_the_last_one_is_stale_B21(int secondsAfterLast)
    {
        var profile = Located();

        Assert.Equal(LocationUpdateResult.Stale, profile.UpdateLocation(21.03m, 105.85m, Now.AddSeconds(secondsAfterLast)));
        AssertUnchanged(profile);
    }

    [Fact]
    public void First_point_is_applied()
    {
        var profile = DriverProfile.Create(1, "001", "B2-001", "front.jpg", "back.jpg");

        Assert.Equal(LocationUpdateResult.Applied, profile.UpdateLocation(21.03m, 105.85m, Now));
    }
}
