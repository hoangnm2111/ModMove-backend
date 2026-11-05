using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

/// <summary>GB-03 (Online only when Approved) and DECISIONS 6.4 / GB-39 (no GPS for 5 minutes = disconnected).</summary>
public class DriverProfileAvailabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static DriverProfile NewProfile() => DriverProfile.Create(1, "001", "B2-001", "front.jpg", "back.jpg");

    private static DriverProfile ApprovedProfile()
    {
        var profile = NewProfile();
        profile.Approve(Vehicle.Create(1, 1, "51C-1", 1000, "250x160x160", "r", "i", "p"));
        return profile;
    }

    [Fact]
    public void Unapproved_profile_cannot_go_online_GB03()
    {
        var profile = NewProfile();

        Assert.Throws<DomainException>(profile.GoOnline);
        Assert.Equal(OnlineStatus.Offline, profile.OnlineStatus);
    }

    [Fact]
    public void Approved_profile_goes_online_and_offline()
    {
        var profile = ApprovedProfile();

        profile.GoOnline();
        Assert.Equal(OnlineStatus.Online, profile.OnlineStatus);

        profile.GoOffline();
        Assert.Equal(OnlineStatus.Offline, profile.OnlineStatus);
    }

    [Fact]
    public void Without_a_gps_point_the_location_is_not_fresh()
    {
        Assert.False(ApprovedProfile().HasFreshLocation(Now));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(299, true)]
    [InlineData(300, false)] // exactly 5 minutes is already disconnected
    [InlineData(301, false)]
    public void A_gps_point_is_fresh_for_less_than_5_minutes_6_4(int ageSeconds, bool fresh)
    {
        var profile = ApprovedProfile();
        profile.UpdateLocation(21.03m, 105.85m, Now.AddSeconds(-ageSeconds));

        Assert.Equal(fresh, profile.HasFreshLocation(Now));
    }

    [Fact]
    public void A_new_gps_point_reconnects_the_driver()
    {
        var profile = ApprovedProfile();
        profile.UpdateLocation(21.03m, 105.85m, Now.AddMinutes(-10));
        Assert.False(profile.HasFreshLocation(Now));

        profile.UpdateLocation(21.04m, 105.86m, Now);

        Assert.True(profile.HasFreshLocation(Now));
    }
}

