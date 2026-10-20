using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

public class UserAndCouponTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static User NewUser() => User.Create("A", "0900000001", "a@x.vn", "hash", UserRole.Customer);

    [Fact]
    public void Five_failed_logins_lock_the_account_for_10_minutes_GB02()
    {
        var user = NewUser();

        for (var i = 0; i < 4; i++)
            user.RegisterFailedLogin(Now);
        Assert.False(user.IsLocked(Now));

        user.RegisterFailedLogin(Now);
        Assert.True(user.IsLocked(Now.AddMinutes(9)));
        Assert.False(user.IsLocked(Now.AddMinutes(10)));
    }

    [Fact]
    public void Successful_login_resets_the_failure_count()
    {
        var user = NewUser();
        user.RegisterFailedLogin(Now);

        user.RegisterSuccessfulLogin();

        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void Otp_can_be_sent_three_times_per_15_minutes_GB25()
    {
        var otp = OtpVerification.Issue("a@x.vn", OtpPurpose.Register, "h1", Now);
        otp.Resend("h2", Now.AddMinutes(1));
        otp.Resend("h3", Now.AddMinutes(2));

        Assert.Throws<DomainException>(() => otp.Resend("h4", Now.AddMinutes(3)));

        otp.Resend("h4", Now.AddMinutes(16)); // new window
        Assert.Equal(1, otp.SendCount);
    }

    [Fact]
    public void Otp_expires_after_5_minutes_GB25()
    {
        var otp = OtpVerification.Issue("a@x.vn", OtpPurpose.Register, "h", Now);

        Assert.False(otp.IsExpired(Now.AddMinutes(4)));
        Assert.True(otp.IsExpired(Now.AddMinutes(5)));
    }

    [Fact]
    public void Coupon_checks_dates_minimum_spend_and_usage_GB21()
    {
        var coupon = Coupon.Create("SAVE20", 20_000, 100_000, Now, Now.AddDays(30), usageLimit: 1);

        coupon.EnsureApplicable(100_000, Now.AddDays(1));
        Assert.Throws<DomainException>(() => coupon.EnsureApplicable(99_999, Now.AddDays(1)));
        Assert.Throws<DomainException>(() => coupon.EnsureApplicable(100_000, Now.AddDays(31)));

        coupon.MarkUsed();
        Assert.Throws<DomainException>(() => coupon.EnsureApplicable(100_000, Now.AddDays(1)));
    }

    [Fact]
    public void Coupon_discount_never_exceeds_the_order()
    {
        var coupon = Coupon.Create("BIG", 100_000, 0, Now, Now.AddDays(1), null);

        Assert.Equal(60_000m, coupon.DiscountFor(60_000));
    }

    [Fact]
    public void Driver_cannot_go_online_before_approval_GB03()
    {
        var profile = DriverProfile.Create(1, "id", "lic", "f", "b");

        Assert.Throws<DomainException>(profile.GoOnline);
    }

    [Fact]
    public void Only_one_driver_application_can_be_pending()
    {
        var profile = DriverProfile.Create(1, "id", "lic", "f", "b");
        profile.Submit(DriverApplication.Create(0, 1, 1, "id", "lic", "51C-1", 500, ["d"], Now));

        Assert.Throws<DomainException>(() =>
            profile.Submit(DriverApplication.Create(0, 2, 1, "id", "lic", "51C-1", 500, ["d"], Now)));
    }

    [Fact]
    public void Rejecting_an_application_needs_a_reason()
    {
        var application = DriverApplication.Create(0, 1, 1, "id", "lic", "51C-1", 500, ["d"], Now);

        Assert.Throws<DomainException>(() => application.Reject(2, [], Now));
    }
}
