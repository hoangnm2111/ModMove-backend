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
        Assert.True(otp.TryResend("h2", Now.AddMinutes(1)));
        Assert.True(otp.TryResend("h3", Now.AddMinutes(2)));

        Assert.False(otp.TryResend("h4", Now.AddMinutes(3)));
        Assert.Equal("h3", otp.CodeHash); // a refused resend leaves the current code alone

        Assert.True(otp.TryResend("h4", Now.AddMinutes(16))); // new window
        Assert.Equal(1, otp.SendCount);
    }

    [Fact]
    public void Otp_stops_working_after_five_wrong_guesses()
    {
        var otp = OtpVerification.Issue("a@x.vn", OtpPurpose.ResetPassword, "h1", Now);

        for (var i = 0; i < 4; i++)
            otp.RegisterFailedAttempt();
        Assert.True(otp.IsUsable(Now));

        otp.RegisterFailedAttempt();
        Assert.False(otp.IsUsable(Now));

        otp.TryResend("h2", Now.AddMinutes(1)); // a new code starts a fresh count
        Assert.True(otp.IsUsable(Now.AddMinutes(1)));
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

    [Theory]
    [InlineData("inactive", "COUPON_INACTIVE")]
    [InlineData("not-started", "COUPON_EXPIRED")]
    [InlineData("expired", "COUPON_EXPIRED")]
    [InlineData("used-up", "COUPON_LIMIT_REACHED")]
    [InlineData("too-small", "COUPON_MIN_ORDER_NOT_MET")]
    public void Coupon_rejections_have_their_own_code_GB21(string scenario, string code)
    {
        var coupon = Coupon.Create("SAVE20", 20_000, 100_000, Now, Now.AddDays(30), usageLimit: 1);
        var at = Now.AddDays(1);
        var orderValue = 100_000m;
        switch (scenario)
        {
            case "inactive": coupon.SetActive(false); break;
            case "not-started": at = Now.AddDays(-1); break;
            case "expired": at = Now.AddDays(31); break;
            case "used-up": coupon.MarkUsed(); break;
            case "too-small": orderValue = 99_999; break;
        }

        var error = Assert.Throws<DomainException>(() => coupon.EnsureApplicable(orderValue, at));

        Assert.Equal(code, error.Code);
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

    [Fact]
    public void Approving_a_newer_version_updates_the_existing_vehicle()
    {
        var profile = DriverProfile.Create(1, "id", "lic", "f", "b");
        profile.Approve(Vehicle.Create(1, 1, "51C-1", 500, "1x1x1", "r", "i", "p"));
        var original = profile.Vehicle;

        profile.Approve(Vehicle.Create(1, 2, "51C-2", 1000, "2x2x2", "r2", "i2", "p2"));

        Assert.Same(original, profile.Vehicle);
        Assert.Equal("51C-2", profile.Vehicle!.LicensePlate);
        Assert.Equal(2, profile.Vehicle.VehicleTypeId);
    }

    [Theory]
    [InlineData("", "0900000009", "Hỏi giá")]
    [InlineData("An", " ", "Hỏi giá")]
    [InlineData("An", "0900000009", "")]
    public void Support_request_needs_name_contact_and_question_B14(string name, string contact, string content)
    {
        Assert.Throws<DomainException>(() => SupportRequest.Submit(name, contact, content));
    }

    [Fact]
    public void Support_request_starts_as_new()
    {
        var request = SupportRequest.Submit(" An ", "0900000009", "Xe tải 1 tấn chở được tủ lạnh không?");

        Assert.Equal(SupportRequestStatus.New, request.Status);
        Assert.Equal("An", request.FullName);
    }

    [Fact]
    public void Rating_is_allowed_within_one_day_of_completion_7_5()
    {
        var rating = Rating.Create(1, 2, 3, 5, "Tốt", orderCompletedAt: Now, now: Now.AddHours(23));

        Assert.Equal(5, rating.RatingStars);
    }

    [Fact]
    public void Rating_after_one_day_is_rejected_7_5()
    {
        var error = Assert.Throws<DomainException>(() => Rating.Create(1, 2, 3, 5, null, Now, Now.AddDays(1).AddMinutes(1)));

        Assert.Equal("RATING_WINDOW_CLOSED", error.Code);
    }

    [Fact]
    public void Rating_needs_a_completed_order_GB20()
    {
        var error = Assert.Throws<DomainException>(() => Rating.Create(1, 2, 3, 5, null, orderCompletedAt: null, Now));

        Assert.Equal("RATING_NOT_ALLOWED", error.Code);
    }

    [Fact]
    public void Customer_answers_a_dispatcher_request_with_text_and_photos_7_4()
    {
        var refund = Refund.Request(1, null, RefundSourceType.Complaint, "Hàng vỡ", ["a.jpg", "b.jpg"], 100_000);
        refund.RequestMoreInfo("Chụp thêm ảnh thùng hàng");

        refund.ReplyToInfoRequest("Đây là ảnh thùng", ["c.jpg"]);

        Assert.Equal("Đây là ảnh thùng", refund.CustomerReply);
        Assert.Equal(["a.jpg", "b.jpg", "c.jpg"], refund.EvidenceUrls!);
    }

    [Fact]
    public void Reply_cannot_push_evidence_past_five_photos_7_4()
    {
        var refund = Refund.Request(1, null, RefundSourceType.Complaint, "Hàng vỡ", ["1", "2", "3", "4"], 100_000);
        refund.RequestMoreInfo("Thêm ảnh");

        var error = Assert.Throws<DomainException>(() => refund.ReplyToInfoRequest("ok", ["5", "6"]));

        Assert.Equal("TOO_MANY_PHOTOS", error.Code);
    }

    [Fact]
    public void Cancellation_refund_goes_straight_to_the_administrator_B9()
    {
        var refund = Refund.Request(1, null, RefundSourceType.Cancellation, "Khách hủy khi Accepted", null, 180_000);

        refund.Approve(adminId: 4, Now);

        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
        Assert.Equal(180_000m, refund.ProposedAmount);
    }

    [Fact]
    public void Completing_a_registration_creates_a_verified_account_and_forgets_the_held_details_A02()
    {
        var otp = OtpVerification.Issue("an@example.com", OtpPurpose.Register, "h", Now);
        otp.HoldRegistration("An", "0912345678", "pw-hash", UserRole.Driver);

        var user = otp.CompleteRegistration(Now);

        Assert.Equal(("An", "0912345678", "an@example.com", "pw-hash", UserRole.Driver), (user.FullName, user.PhoneNumber, user.Email, user.PasswordHash, user.Role));
        Assert.True(user.EmailVerified);
        Assert.False(otp.IsUsable(Now));
        Assert.Null(otp.PasswordHash);
        Assert.Null(otp.PhoneNumber);
    }

    [Fact]
    public void A_code_without_held_details_cannot_complete_a_registration()
    {
        var otp = OtpVerification.Issue("an@example.com", OtpPurpose.Register, "h", Now);

        Assert.Throws<InvalidOperationException>(() => otp.CompleteRegistration(Now));
        Assert.True(otp.IsUsable(Now));
    }

    private static Refund OpenComplaint() => Refund.Request(1, null, RefundSourceType.Complaint, "Hàng vỡ", null, 100_000);

    private static Refund ProposedComplaint()
    {
        var refund = OpenComplaint();
        refund.Propose(50_000, "Một phần", 3);
        return refund;
    }

    private static Refund ApprovedComplaint()
    {
        var refund = ProposedComplaint();
        refund.Approve(4, Now);
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now); // R-01: payout needs an account
        return refund;
    }

    public static TheoryData<string, Func<Refund>, Action<Refund>> RefundChanges => new()
    {
        { "RequestMoreInfo", OpenComplaint, r => r.RequestMoreInfo("Chụp thêm ảnh") },
        { "ReplyToInfoRequest", () => { var r = OpenComplaint(); r.RequestMoreInfo("Chụp thêm ảnh"); return r; }, r => r.ReplyToInfoRequest("Đây", null) },
        { "Propose", OpenComplaint, r => r.Propose(50_000, "Một phần", 3) },
        { "Approve", ProposedComplaint, r => r.Approve(4, Now) },
        { "RejectRefund", OpenComplaint, r => r.RejectRefund(4, Now) },
        { "MarkProcessed", ApprovedComplaint, r => r.MarkProcessed("BANK1", Now) },
        { "MarkFailed", ApprovedComplaint, r => r.MarkFailed("Sai số tài khoản") },
        { "SetPayoutAccount", OpenComplaint, r => r.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now) },
        { "Withdraw", OpenComplaint, r => r.Withdraw() },
    };

    [Theory]
    [MemberData(nameof(RefundChanges))]
    public void Every_refund_change_bumps_the_row_version(string _, Func<Refund> create, Action<Refund> change)
    {
        var refund = create();
        var before = refund.RowVersion;

        change(refund);

        Assert.Equal(before + 1, refund.RowVersion);
    }

    [Fact]
    public void Administrator_note_is_kept_on_approval_and_rejection_CUS23()
    {
        var approved = Refund.Request(1, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000);
        var rejected = Refund.Request(2, null, RefundSourceType.Complaint, "Hàng vỡ", null, 100_000);

        approved.Approve(adminId: 4, Now, "Đã đối chiếu giao dịch");
        rejected.RejectRefund(adminId: 4, Now, "Ảnh không cho thấy hư hỏng");

        Assert.Equal("Đã đối chiếu giao dịch", approved.DecisionNote);
        Assert.Equal("Ảnh không cho thấy hư hỏng", rejected.DecisionNote);
    }

    [Fact]
    public void Dispatcher_cannot_change_a_cancellation_refund_B9()
    {
        var refund = Refund.Request(1, null, RefundSourceType.Cancellation, "Khách hủy khi Accepted", null, 180_000);

        var propose = Assert.Throws<DomainException>(() => refund.Propose(50_000, "giảm", dispatcherId: 3));
        var askInfo = Assert.Throws<DomainException>(() => refund.RequestMoreInfo("Gửi thêm ảnh"));

        Assert.Equal("REFUND_NOT_FOR_DISPATCHER", propose.Code);
        Assert.Equal("REFUND_NOT_FOR_DISPATCHER", askInfo.Code);
        Assert.Equal(180_000m, refund.ProposedAmount);
    }

    [Fact]
    public void Complaint_refund_still_needs_a_dispatcher_proposal_B9()
    {
        var refund = Refund.Request(1, null, RefundSourceType.Complaint, "Hàng vỡ", null, 100_000);

        Assert.Throws<DomainException>(() => refund.Approve(adminId: 4, Now));
    }

    [Fact]
    public void Reply_without_a_request_is_rejected()
    {
        var refund = Refund.Request(1, null, RefundSourceType.Complaint, "Hàng vỡ", null, 100_000);

        Assert.Throws<DomainException>(() => refund.ReplyToInfoRequest("ok", null));
    }

    [Fact]
    public void User_change_status_updates_account_status()
    {
        var user = NewUser();
        Assert.Equal(AccountStatus.Active, user.AccountStatus);

        user.ChangeStatus(AccountStatus.Locked);
        Assert.Equal(AccountStatus.Locked, user.AccountStatus);

        user.ChangeStatus(AccountStatus.Inactive);
        Assert.Equal(AccountStatus.Inactive, user.AccountStatus);

        user.ChangeStatus(AccountStatus.Active);
        Assert.Equal(AccountStatus.Active, user.AccountStatus);
    }

    [Fact]
    public void Customer_withdraws_a_complaint_still_under_review_B9()
    {
        var refund = Refund.Request(1, null, RefundSourceType.Complaint, "Hàng vỡ", null, 100_000);

        refund.Withdraw();

        Assert.Equal(RefundStatus.Withdrawn, refund.RefundStatus);
        Assert.Throws<DomainException>(refund.Withdraw);
    }

    [Fact]
    public void A_cancellation_refund_cannot_be_withdrawn_B9()
    {
        var refund = Refund.Request(1, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000);

        Assert.Throws<DomainException>(refund.Withdraw);
        Assert.Equal(RefundStatus.Requested, refund.RefundStatus);
    }

    [Fact]
    public void A_second_question_waits_for_a_new_answer_7_4()
    {
        var refund = Refund.Request(1, null, RefundSourceType.Complaint, "Hàng vỡ", null, 100_000);
        refund.RequestMoreInfo("Chụp thêm ảnh");
        refund.ReplyToInfoRequest("Đây", null);

        refund.RequestMoreInfo("Chụp mặt đáy thùng");

        Assert.Null(refund.CustomerReply);
        Assert.Equal("Chụp mặt đáy thùng", refund.InfoRequestNote);
    }

    [Fact]
    public void A_decided_refund_cannot_be_withdrawn()
    {
        var refund = Refund.Request(1, null, RefundSourceType.Complaint, "Hàng vỡ", null, 100_000);
        refund.Propose(50_000, "Một phần", dispatcherId: 3);
        refund.Approve(adminId: 4, Now);

        Assert.Throws<DomainException>(refund.Withdraw);
        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
    }
}
