using FluentValidation.TestHelper;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Auth.Commands.ChangePassword;
using ModMove.Application.Features.Auth.Commands.Login;
using ModMove.Application.Features.Auth.Commands.Logout;
using ModMove.Application.Features.Auth.Commands.Refresh;
using ModMove.Application.Features.Auth.Commands.Register;
using ModMove.Application.Features.Auth.Commands.ResendOtp;
using ModMove.Application.Features.Auth.Commands.ResetPassword;
using ModMove.Application.Features.Auth.Commands.VerifyEmail;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;
using ModMove.Application.Features.Orders.Commands.CancelOrder;
using ModMove.Application.Features.Orders.Commands.CreateOrder;
using ModMove.Application.Features.Orders.Commands.EstimateFare;
using ModMove.Application.Features.Orders.Commands.ReviewPickupEvidence;
using ModMove.Application.Features.Orders.Commands.UploadImage;
using ModMove.Application.Features.Orders.Queries.GetMyOrders;
using ModMove.Application.Features.Ratings.Commands.RateTrip;
using ModMove.Application.Features.Ratings.Queries.GetMyRatings;
using ModMove.Application.Features.Refunds;
using ModMove.Application.Features.Refunds.Commands.ReplyToInfoRequest;
using ModMove.Application.Features.Refunds.Commands.RequestIncidentRefund;
using ModMove.Application.Features.Refunds.Commands.SubmitComplaint;
using ModMove.Application.Features.Refunds.Commands.WithdrawRefund;
using ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;
using ModMove.Application.Features.Tracking.Queries.CanFollowOrder;
using ModMove.Application.Features.Tracking.Queries.GetOrderTracking;
using ModMove.Application.Features.Users.Commands.UpdateFcmToken;
using ModMove.Application.Features.Users.Commands.UpdateMyProfile;
using ModMove.Domain.Enums;

namespace ModMove.UnitTests.Application;

/// <summary>
/// Input validation rules of the Guest and Customer use cases (Auth, Orders, Ratings, Refunds and Incidents, Tracking).
/// Each case feeds the validator one input and checks which field is refused. Boundary values sit on both sides of every limit.
/// </summary>
public class CustomerValidatorTests
{
    private const string Photo = "https://res.cloudinary.com/modmove/image/upload/a.jpg";

    private sealed class TeamStorage : IFileStorage
    {
        public Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public bool IsOwnedUrl(string url) => url.StartsWith("https://res.cloudinary.com/modmove/", StringComparison.Ordinal);
    }

    private static readonly IFileStorage Storage = new TeamStorage();

    private static string Chars(int length) => new('a', length);

    // ------------------------------------------------------------------ Auth

    private static RegisterCommand Register(
        string name = "Nguyen Van A", string phone = "0912345678", string email = "a@example.com",
        string password = "abcd1234", UserRole role = UserRole.Customer) => new(name, phone, email, password, role);

    [Fact]
    public void Register_with_valid_data_passes_UC_GST_01() =>
        new RegisterCommandValidator().TestValidate(Register()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("A", true)]
    public void Register_name_is_required(string name, bool valid)
    {
        var result = new RegisterCommandValidator().TestValidate(Register(name: name));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.FullName);
        else result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Register_name_is_at_most_100_characters(int length, bool valid)
    {
        var result = new RegisterCommandValidator().TestValidate(Register(name: Chars(length)));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.FullName);
        else result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Theory]
    [InlineData("0912345678", true)]
    [InlineData("091234567", false)]    // 9 digits
    [InlineData("09123456789", false)]  // 11 digits
    [InlineData("1912345678", false)]   // must start with 0
    [InlineData("09x2345678", false)]
    [InlineData("", false)]
    public void Register_phone_is_10_digits_starting_with_0(string phone, bool valid)
    {
        var result = new RegisterCommandValidator().TestValidate(Register(phone: phone));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
        else result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Theory]
    [InlineData("a@example.com", true)]
    [InlineData("invalid-email", false)]
    [InlineData("a@", false)]
    [InlineData("", false)]
    public void Register_email_must_have_a_valid_format(string email, bool valid)
    {
        var result = new RegisterCommandValidator().TestValidate(Register(email: email));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Register_email_is_at_most_100_characters()
    {
        var atLimit = Chars(88) + "@example.com";   // 100 characters
        var overLimit = Chars(89) + "@example.com";  // 101 characters

        new RegisterCommandValidator().TestValidate(Register(email: atLimit)).ShouldNotHaveValidationErrorFor(x => x.Email);
        new RegisterCommandValidator().TestValidate(Register(email: overLimit)).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("abcd123", false)]   // 7 characters
    [InlineData("abcd1234", true)]   // 8 characters, the shortest allowed
    [InlineData("abcdefgh", false)]  // no digit
    [InlineData("12345678", false)]  // no letter
    [InlineData("", false)]
    public void Register_password_follows_GB_53(string password, bool valid)
    {
        var result = new RegisterCommandValidator().TestValidate(Register(password: password));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Password);
        else result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Register_password_is_at_most_100_characters_GB_53()
    {
        var atLimit = Chars(96) + "1234";
        var overLimit = Chars(97) + "1234";

        new RegisterCommandValidator().TestValidate(Register(password: atLimit)).ShouldNotHaveValidationErrorFor(x => x.Password);
        new RegisterCommandValidator().TestValidate(Register(password: overLimit)).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData(UserRole.Customer, true)]
    [InlineData(UserRole.Driver, true)]
    [InlineData(UserRole.Dispatcher, false)]
    [InlineData(UserRole.Administrator, false)]
    public void Register_only_creates_customers_or_drivers_GB_45(UserRole role, bool valid)
    {
        var result = new RegisterCommandValidator().TestValidate(Register(role: role));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Role);
        else result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData("", "secret123")]
    [InlineData("a@example.com", "")]
    public void Login_needs_an_email_and_a_password_UC_CUS_01(string email, string password) =>
        new LoginCommandValidator().TestValidate(new LoginCommand(email, password)).ShouldHaveAnyValidationError();

    [Fact]
    public void Login_with_both_fields_passes_UC_CUS_01() =>
        new LoginCommandValidator().TestValidate(new LoginCommand("a@example.com", "secret123")).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("123456", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("abcdef", false)]
    [InlineData("", false)]
    public void Verify_email_code_is_exactly_6_digits_GB_25(string code, bool valid)
    {
        var result = new VerifyEmailCommandValidator().TestValidate(new VerifyEmailCommand("a@example.com", code, "secret123"));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Code);
        else result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Theory]
    [InlineData("123456", "abcd1234", true)]
    [InlineData("12345", "abcd1234", false)]
    [InlineData("123456", "short1", false)]
    public void Reset_password_needs_a_6_digit_code_and_a_valid_new_password_UC_CUS_02(string code, string password, bool valid)
    {
        var result = new ResetPasswordCommandValidator().TestValidate(new ResetPasswordCommand("a@example.com", code, password));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData("old12345", "new12345", true)]
    [InlineData("", "new12345", false)]
    [InlineData("old12345", "newpass", false)]   // 7 characters, no digit
    public void Change_password_needs_the_current_one_and_a_valid_new_one_UC_CUS_04(string current, string next, bool valid)
    {
        var result = new ChangePasswordCommandValidator().TestValidate(new ChangePasswordCommand(current, next));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    [InlineData(0, false)]
    public void Refresh_token_is_required_and_at_most_100_characters_GB_47(int length, bool valid)
    {
        var result = new RefreshTokenCommandValidator().TestValidate(new RefreshTokenCommand(Chars(length)));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Resend_otp_purpose_must_be_a_known_value() =>
        new ResendOtpCommandValidator().TestValidate(new ResendOtpCommand("a@example.com", (OtpPurpose)99))
            .ShouldHaveValidationErrorFor(x => x.Purpose);

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    public void Logout_fcm_token_is_at_most_255_characters_GB_48(int length, bool valid)
    {
        var result = new LogoutCommandValidator().TestValidate(new LogoutCommand(Chars(length)));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor(x => x.FcmToken);
    }

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    [InlineData(0, false)]
    public void Fcm_token_is_required_and_at_most_255_characters_GB_48(int length, bool valid)
    {
        var result = new UpdateFcmTokenCommandValidator().TestValidate(new UpdateFcmTokenCommand(Chars(length)));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData("Nguyen Van A", "0912345678", true)]
    [InlineData("", "0912345678", false)]
    [InlineData("Nguyen Van A", "123", false)]
    public void Update_profile_checks_name_and_phone_UC_CUS_06(string name, string phone, bool valid)
    {
        var result = new UpdateMyProfileCommandValidator().TestValidate(new UpdateMyProfileCommand(name, phone));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    // ---------------------------------------------------------------- Orders

    private static CargoInput Cargo(
        string type = "FURNITURE", int quantity = 1, decimal weight = 50, int length = 100, int width = 60, int height = 50,
        IReadOnlyList<string>? photos = null, string? notes = null) =>
        new(type, quantity, weight, length, width, height, photos ?? [Photo], notes);

    private static CreateOrderCommand Order(
        CargoInput? cargo = null, string pickup = "12 Nguyen Hue, Quan 1", string dropoff = "45 Le Van Viet, Thu Duc",
        string receiverName = "Nguyen Van B", string receiverPhone = "0911000001", PaymentMethod method = PaymentMethod.Cash,
        PayerRole? payer = PayerRole.Sender, bool commitment = true, decimal? expected = null, string? coupon = null) =>
        new(1, pickup, dropoff, receiverName, receiverPhone, cargo ?? Cargo(), false, coupon, method, payer, commitment, expected);

    private static TestValidationResult<CreateOrderCommand> CheckOrder(CreateOrderCommand command) =>
        new CreateOrderCommandValidator(Storage).TestValidate(command);

    [Fact]
    public void Create_order_with_valid_cash_data_passes_UC_GST_17() =>
        CheckOrder(Order()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Create_order_with_valid_PayOS_data_passes_UC_GST_17() =>
        CheckOrder(Order(method: PaymentMethod.PayOS, payer: null)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Create_order_needs_the_no_prohibited_goods_commitment_GB_07() =>
        CheckOrder(Order(commitment: false)).ShouldHaveValidationErrorFor(x => x.ProhibitedGoodsCommitment);

    [Theory]
    [InlineData(PaymentMethod.Cash, null, false)]
    [InlineData(PaymentMethod.Cash, PayerRole.Sender, true)]
    [InlineData(PaymentMethod.Cash, PayerRole.Receiver, true)]
    [InlineData(PaymentMethod.PayOS, PayerRole.Sender, false)]
    [InlineData(PaymentMethod.PayOS, null, true)]
    public void Create_order_payer_depends_on_the_payment_method_GB_40(PaymentMethod method, PayerRole? payer, bool valid)
    {
        var result = CheckOrder(Order(method: method, payer: payer));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.PayerRole);
        else result.ShouldHaveValidationErrorFor(x => x.PayerRole);
    }

    [Fact]
    public void Create_order_payment_method_must_be_known() =>
        CheckOrder(Order(method: (PaymentMethod)99)).ShouldHaveValidationErrorFor(x => x.PaymentMethod);

    [Theory]
    [InlineData("0911000001", true)]
    [InlineData("123", false)]
    [InlineData("091100000", false)]
    [InlineData("", false)]
    public void Create_order_receiver_phone_follows_UC_GST_09(string phone, bool valid)
    {
        var result = CheckOrder(Order(receiverPhone: phone));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.ReceiverPhone);
        else result.ShouldHaveValidationErrorFor(x => x.ReceiverPhone);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    [InlineData(0, false)]
    public void Create_order_receiver_name_limits(int length, bool valid)
    {
        var result = CheckOrder(Order(receiverName: Chars(length)));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.ReceiverName);
        else result.ShouldHaveValidationErrorFor(x => x.ReceiverName);
    }

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    [InlineData(0, false)]
    public void Create_order_address_limits_UC_GST_07_08(int length, bool valid)
    {
        var pickup = CheckOrder(Order(pickup: Chars(length)));
        var dropoff = CheckOrder(Order(dropoff: Chars(length)));

        if (valid)
        {
            pickup.ShouldNotHaveValidationErrorFor(x => x.PickupAddress);
            dropoff.ShouldNotHaveValidationErrorFor(x => x.DropoffAddress);
        }
        else
        {
            pickup.ShouldHaveValidationErrorFor(x => x.PickupAddress);
            dropoff.ShouldHaveValidationErrorFor(x => x.DropoffAddress);
        }
    }

    [Theory]
    [InlineData(0.01, true)]
    [InlineData(100000, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(100001, false)]
    public void Create_order_cargo_weight_limits_UC_GST_10(double weight, bool valid)
    {
        var result = CheckOrder(Order(cargo: Cargo(weight: (decimal)weight)));

        if (valid) result.ShouldNotHaveValidationErrorFor("Cargo.WeightKg");
        else result.ShouldHaveValidationErrorFor("Cargo.WeightKg");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2000, true)]
    [InlineData(0, false)]
    [InlineData(2001, false)]
    public void Create_order_each_cargo_dimension_is_1_to_2000_cm_UC_GST_10(int size, bool valid)
    {
        var length = CheckOrder(Order(cargo: Cargo(length: size)));
        var width = CheckOrder(Order(cargo: Cargo(width: size)));
        var height = CheckOrder(Order(cargo: Cargo(height: size)));

        if (valid)
        {
            length.ShouldNotHaveValidationErrorFor("Cargo.LengthCm");
            width.ShouldNotHaveValidationErrorFor("Cargo.WidthCm");
            height.ShouldNotHaveValidationErrorFor("Cargo.HeightCm");
        }
        else
        {
            length.ShouldHaveValidationErrorFor("Cargo.LengthCm");
            width.ShouldHaveValidationErrorFor("Cargo.WidthCm");
            height.ShouldHaveValidationErrorFor("Cargo.HeightCm");
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Create_order_cargo_quantity_must_be_positive(int quantity, bool valid)
    {
        var result = CheckOrder(Order(cargo: Cargo(quantity: quantity)));

        if (valid) result.ShouldNotHaveValidationErrorFor("Cargo.Quantity");
        else result.ShouldHaveValidationErrorFor("Cargo.Quantity");
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Create_order_special_notes_are_at_most_500_characters(int length, bool valid)
    {
        var result = CheckOrder(Order(cargo: Cargo(notes: Chars(length))));

        if (valid) result.ShouldNotHaveValidationErrorFor("Cargo.SpecialNotes");
        else result.ShouldHaveValidationErrorFor("Cargo.SpecialNotes");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Create_order_has_1_to_5_cargo_photos_GB_06(int count, bool valid)
    {
        var result = CheckOrder(Order(cargo: Cargo(photos: Enumerable.Repeat(Photo, count).ToList())));

        if (valid) result.ShouldNotHaveValidationErrorFor("Cargo.PhotoUrls");
        else result.ShouldHaveValidationErrorFor("Cargo.PhotoUrls");
    }

    [Fact]
    public void Create_order_photos_must_come_from_the_team_cloud_GB_06() =>
        CheckOrder(Order(cargo: Cargo(photos: ["https://evil.example/a.jpg"]))).ShouldHaveAnyValidationError();

    [Theory]
    [InlineData(null, true)]
    [InlineData(0.0, true)]
    [InlineData(250000.0, true)]
    [InlineData(-1.0, false)]
    [InlineData(1000.5, false)]
    public void Create_order_expected_total_is_a_whole_non_negative_amount_4_4(double? fee, bool valid)
    {
        var result = CheckOrder(Order(expected: fee is null ? null : (decimal)fee));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.ExpectedTotalFee);
        else result.ShouldHaveValidationErrorFor(x => x.ExpectedTotalFee);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void Create_order_coupon_code_is_at_most_50_characters(int length, bool valid)
    {
        var result = CheckOrder(Order(coupon: Chars(length)));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.CouponCode);
        else result.ShouldHaveValidationErrorFor(x => x.CouponCode);
    }

    private static EstimateFareCommand Estimate(
        long vehicle = 1, string pickup = "12 Nguyen Hue", string dropoff = "45 Le Van Viet", decimal weight = 100,
        int length = 100, int width = 60, int height = 50, string? coupon = null) =>
        new(vehicle, pickup, dropoff, weight, length, width, height, false, coupon);

    [Fact]
    public void Estimate_with_valid_data_passes_UC_GST_13() =>
        new EstimateFareCommandValidator().TestValidate(Estimate()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0, "a", "b", 100, 1, 1, 1, null)]
    [InlineData(1, "", "b", 100, 1, 1, 1, null)]
    [InlineData(1, "a", "", 100, 1, 1, 1, null)]
    [InlineData(1, "a", "b", 0, 1, 1, 1, null)]
    [InlineData(1, "a", "b", 100001, 1, 1, 1, null)]
    [InlineData(1, "a", "b", 100, 0, 1, 1, null)]
    [InlineData(1, "a", "b", 100, 1, 2001, 1, null)]
    [InlineData(1, "a", "b", 100, 1, 1, 0, null)]
    public void Estimate_refuses_out_of_range_input_UC_GST_13(
        long vehicle, string pickup, string dropoff, double weight, int length, int width, int height, string? coupon) =>
        new EstimateFareCommandValidator().TestValidate(Estimate(vehicle, pickup, dropoff, (decimal)weight, length, width, height, coupon))
            .ShouldHaveAnyValidationError();

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    public void Estimate_address_and_coupon_length_limits(int length, bool valid)
    {
        var address = new EstimateFareCommandValidator().TestValidate(Estimate(pickup: Chars(length)));
        var coupon = new EstimateFareCommandValidator().TestValidate(Estimate(coupon: Chars(length == 255 ? 50 : 51)));

        if (valid)
        {
            address.ShouldNotHaveValidationErrorFor(x => x.PickupAddress);
            coupon.ShouldNotHaveValidationErrorFor(x => x.CouponCode);
        }
        else
        {
            address.ShouldHaveValidationErrorFor(x => x.PickupAddress);
            coupon.ShouldHaveValidationErrorFor(x => x.CouponCode);
        }
    }

    [Theory]
    [InlineData(1, "CHANGED_PLAN", null, true)]
    [InlineData(0, "CHANGED_PLAN", null, false)]
    [InlineData(1, "", null, false)]
    [InlineData(1, "OTHER", 500, true)]
    [InlineData(1, "OTHER", 501, false)]
    public void Cancel_order_needs_an_id_a_reason_code_and_a_reason_of_at_most_500_UC_CUS_29(
        long orderId, string code, int? reasonLength, bool valid)
    {
        var reason = reasonLength is null ? null : Chars(reasonLength.Value);

        var result = new CancelOrderCommandValidator().TestValidate(new CancelOrderCommand(orderId, code, reason));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, "Photos are blurry", true)]
    [InlineData(false, null, false)]
    [InlineData(false, "  ", false)]
    public void Rejecting_pickup_photos_needs_a_reason_UC_CUS_12(bool approve, string? reason, bool valid)
    {
        var result = new ReviewPickupEvidenceCommandValidator().TestValidate(new ReviewPickupEvidenceCommand(1, approve, reason));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Pickup_review_reason_is_at_most_500_characters(int length, bool valid)
    {
        var result = new ReviewPickupEvidenceCommandValidator().TestValidate(new ReviewPickupEvidenceCommand(1, false, Chars(length)));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Reason);
        else result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    private static MemoryStream Bytes(params byte[] bytes) => new(bytes);

    private static readonly byte[] JpgHead = [0xFF, 0xD8, 0xFF, 0xE0];
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47];
    private const long FiveMb = 5L * 1024 * 1024;

    [Theory]
    [InlineData(1, true)]
    [InlineData(FiveMb, true)]
    [InlineData(FiveMb + 1, false)]
    [InlineData(0, false)]
    public void Upload_size_is_1_byte_to_5_MB_GB_06(long length, bool valid)
    {
        var command = new UploadImageCommand(Bytes(JpgHead), "photo.jpg", "image/jpeg", length);
        var result = new UploadImageCommandValidator().TestValidate(command);

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Length);
        else result.ShouldHaveValidationErrorFor(x => x.Length);
    }

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/gif", false)]
    [InlineData("application/pdf", false)]
    public void Upload_accepts_only_jpeg_and_png_content_types_GB_06(string contentType, bool valid)
    {
        var result = new UploadImageCommandValidator().TestValidate(new UploadImageCommand(Bytes(JpgHead), "photo", contentType, 100));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.ContentType);
        else result.ShouldHaveValidationErrorFor(x => x.ContentType);
    }

    [Fact]
    public void Upload_checks_the_first_bytes_of_a_JPG()
    {
        var result = new UploadImageCommandValidator().TestValidate(new UploadImageCommand(Bytes(JpgHead), "a.jpg", "image/jpeg", 4));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Upload_checks_the_first_bytes_of_a_PNG()
    {
        var result = new UploadImageCommandValidator().TestValidate(new UploadImageCommand(Bytes(PngHead), "a.png", "image/png", 4));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Upload_refuses_an_executable_renamed_to_jpg_GB_06()
    {
        var exe = Bytes(0x4D, 0x5A, 0x90, 0x00); // "MZ" header of a Windows program

        var result = new UploadImageCommandValidator().TestValidate(new UploadImageCommand(exe, "photo.jpg", "image/jpeg", 4));

        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Upload_refuses_a_file_shorter_than_the_signature()
    {
        var result = new UploadImageCommandValidator().TestValidate(new UploadImageCommand(Bytes(0xFF, 0xD8), "a.jpg", "image/jpeg", 2));

        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Theory]
    [InlineData(1, 20, true)]
    [InlineData(10_000, 50, true)]
    [InlineData(0, 20, false)]
    [InlineData(10_001, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 51, false)]
    public void My_orders_paging_limits_UC_CUS_08(int page, int pageSize, bool valid)
    {
        var result = new GetMyOrdersQueryValidator().TestValidate(new GetMyOrdersQuery(page, pageSize));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData(2020, 1, 1, 2020, 1, 2, true)]
    [InlineData(2026, 11, 5, 2026, 11, 5, true)]
    [InlineData(2026, 11, 6, 2026, 11, 5, false)]   // from after to
    [InlineData(2019, 12, 31, 2026, 1, 1, false)]
    [InlineData(2026, 1, 1, 2101, 1, 1, false)]
    public void My_orders_date_filter_limits_UC_CUS_09(int fy, int fm, int fd, int ty, int tm, int td, bool valid)
    {
        var query = new GetMyOrdersQuery(FromDate: new DateOnly(fy, fm, fd), ToDate: new DateOnly(ty, tm, td));
        var result = new GetMyOrdersQueryValidator().TestValidate(query);

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData("0911000001", true)]
    [InlineData("091", true)]            // part of a number is enough to search
    [InlineData("012345678901234", true)] // 15 digits
    [InlineData("0123456789012345", false)] // 16 digits
    [InlineData("09a", false)]
    public void My_orders_receiver_phone_filter_is_1_to_15_digits_UC_CUS_09(string phone, bool valid)
    {
        var result = new GetMyOrdersQueryValidator().TestValidate(new GetMyOrdersQuery(ReceiverPhone: phone));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.ReceiverPhone);
        else result.ShouldHaveValidationErrorFor(x => x.ReceiverPhone);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void My_orders_search_text_is_at_most_100_characters(int length, bool valid)
    {
        var result = new GetMyOrdersQueryValidator().TestValidate(new GetMyOrdersQuery(Search: Chars(length)));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Search);
        else result.ShouldHaveValidationErrorFor(x => x.Search);
    }

    [Fact]
    public void My_orders_status_filter_must_be_a_known_status() =>
        new GetMyOrdersQueryValidator().TestValidate(new GetMyOrdersQuery(Status: (OrderStatus)99)).ShouldHaveValidationErrorFor(x => x.Status);

    // --------------------------------------------------------------- Ratings

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    [InlineData(-1, false)]
    public void Rating_stars_are_1_to_5_GB_20(int stars, bool valid)
    {
        var result = new RateTripCommandValidator().TestValidate(new RateTripCommand(1, stars, null));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Stars);
        else result.ShouldHaveValidationErrorFor(x => x.Stars);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Rating_comment_is_at_most_1000_characters_UC_CUS_25(int? length, bool valid)
    {
        var comment = length is null ? null : Chars(length.Value);

        var result = new RateTripCommandValidator().TestValidate(new RateTripCommand(1, 5, comment));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Comment);
        else result.ShouldHaveValidationErrorFor(x => x.Comment);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-3, false)]
    public void Rating_needs_a_real_order_id(long orderId, bool valid)
    {
        var result = new RateTripCommandValidator().TestValidate(new RateTripCommand(orderId, 5, null));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.OrderId);
        else result.ShouldHaveValidationErrorFor(x => x.OrderId);
    }

    [Theory]
    [InlineData(1, 20, true)]
    [InlineData(10_000, 50, true)]
    [InlineData(0, 20, false)]
    [InlineData(10_001, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 51, false)]
    public void My_ratings_paging_limits_UC_CUS_26(int page, int pageSize, bool valid)
    {
        var result = new GetMyRatingsQueryValidator().TestValidate(new GetMyRatingsQuery(page, pageSize));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    // ------------------------------------------------- Refunds and incidents

    private static readonly PayoutAccountInput ValidPayout = new(false, "970436", "0123456789", "NGUYEN VAN A");

    private static SubmitComplaintCommand Complaint(
        string code = "DAMAGED_CARGO", string? reason = null, IReadOnlyList<string>? evidence = null, decimal amount = 100_000) =>
        new(1, code, reason, evidence, amount, ValidPayout);

    private static TestValidationResult<SubmitComplaintCommand> CheckComplaint(SubmitComplaintCommand c) =>
        new SubmitComplaintCommandValidator(Storage).TestValidate(c);

    [Fact]
    public void Complaint_with_valid_data_passes_UC_CUS_21() =>
        CheckComplaint(Complaint(evidence: [Photo])).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(100000, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(100000.5, false)]
    [InlineData(10000000000000, false)]
    public void Complaint_requested_amount_is_a_positive_whole_number_B9(double amount, bool valid)
    {
        var result = CheckComplaint(Complaint(amount: (decimal)amount));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.RequestedAmount);
        else result.ShouldHaveValidationErrorFor(x => x.RequestedAmount);
    }

    [Theory]
    [InlineData("DAMAGED_CARGO", true)]
    [InlineData("", false)]
    public void Complaint_needs_a_reason_code_UC_CUS_21(string code, bool valid)
    {
        var result = CheckComplaint(Complaint(code: code));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.ReasonCode);
        else result.ShouldHaveValidationErrorFor(x => x.ReasonCode);
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Complaint_reason_text_is_at_most_1000_characters(int length, bool valid)
    {
        var result = CheckComplaint(Complaint(reason: Chars(length)));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Reason);
        else result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Complaint_evidence_is_at_most_5_photos_UC_CUS_22(int count, bool valid)
    {
        var result = CheckComplaint(Complaint(evidence: Enumerable.Repeat(Photo, count).ToList()));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.EvidenceUrls);
        else result.ShouldHaveValidationErrorFor(x => x.EvidenceUrls);
    }

    [Fact]
    public void Complaint_evidence_must_come_from_the_team_cloud_GB_06() =>
        CheckComplaint(Complaint(evidence: ["https://evil.example/a.jpg"])).ShouldHaveValidationErrorFor(x => x.EvidenceUrls);

    [Theory]
    [InlineData("Water damage", 100000, true)]
    [InlineData("", 100000, false)]
    [InlineData("Water damage", 0, false)]
    public void Incident_refund_needs_a_reason_and_a_valid_amount_UC_CUS_17(string reason, double amount, bool valid)
    {
        var command = new RequestIncidentRefundCommand(1, reason, null, (decimal)amount, ValidPayout);

        var result = new RequestIncidentRefundCommandValidator(Storage).TestValidate(command);

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Incident_refund_reason_is_at_most_1000_characters(int length, bool valid)
    {
        var result = new RequestIncidentRefundCommandValidator(Storage)
            .TestValidate(new RequestIncidentRefundCommand(1, Chars(length), null, 100_000, ValidPayout));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Reason);
        else result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Theory]
    [InlineData("Here is the photo", 0, true)]
    [InlineData("", 0, false)]
    [InlineData(" ", 0, false)]
    [InlineData("Here is the photo", 6, false)]
    public void Reply_needs_text_and_at_most_5_photos_7_4(string reply, int photos, bool valid)
    {
        var urls = Enumerable.Repeat(Photo, photos).ToList();

        var result = new ReplyToInfoRequestCommandValidator(Storage).TestValidate(new ReplyToInfoRequestCommand(1, reply, urls));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void Withdraw_complaint_needs_a_real_refund_id_UC_CUS_24(long refundId, bool valid)
    {
        var result = new WithdrawRefundCommandValidator().TestValidate(new WithdrawRefundCommand(refundId));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor(x => x.RefundId);
    }

    private static ReportIncidentByCustomerCommand Incident(
        IncidentType type = IncidentType.Other, string description = "Driver is late", IReadOnlyList<string>? photos = null,
        decimal? lat = null, decimal? lng = null) => new(1, type, description, photos, lat, lng);

    private static TestValidationResult<ReportIncidentByCustomerCommand> CheckIncident(ReportIncidentByCustomerCommand c) =>
        new ReportIncidentByCustomerCommandValidator(Storage).TestValidate(c);

    [Fact]
    public void Incident_report_with_valid_data_passes_UC_CUS_15() =>
        CheckIncident(Incident(photos: [Photo], lat: 10.78m, lng: 106.7m)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    [InlineData(0, false)]
    public void Incident_description_is_required_and_at_most_1000_characters(int length, bool valid)
    {
        var result = CheckIncident(Incident(description: Chars(length)));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Description);
        else result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Incident_type_must_be_a_known_value_7_1() =>
        CheckIncident(Incident(type: (IncidentType)99)).ShouldHaveValidationErrorFor(x => x.IncidentType);

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Incident_photos_are_at_most_5_UC_CUS_15(int count, bool valid)
    {
        var result = CheckIncident(Incident(photos: Enumerable.Repeat(Photo, count).ToList()));

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.PhotoUrls);
        else result.ShouldHaveValidationErrorFor(x => x.PhotoUrls);
    }

    [Theory]
    [InlineData(90, 180, true)]
    [InlineData(-90, -180, true)]
    [InlineData(90.1, 0, false)]
    [InlineData(-90.1, 0, false)]
    [InlineData(0, 180.1, false)]
    [InlineData(0, -180.1, false)]
    public void Incident_coordinates_are_real_positions(double lat, double lng, bool valid)
    {
        var result = CheckIncident(Incident(lat: (decimal)lat, lng: (decimal)lng));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData(10.78, null, false)]
    [InlineData(null, 106.7, false)]
    [InlineData(10.78, 106.7, true)]
    [InlineData(null, null, true)]
    public void Incident_coordinates_come_as_a_pair_or_not_at_all(double? lat, double? lng, bool valid)
    {
        var result = CheckIncident(Incident(lat: (decimal?)lat, lng: (decimal?)lng));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor("Coordinates");
    }

    // -------------------------------------------------------------- Tracking

    [Theory]
    [InlineData(90, 180, true)]
    [InlineData(-90, -180, true)]
    [InlineData(0, 0, true)]
    [InlineData(90.0001, 0, false)]
    [InlineData(-90.0001, 0, false)]
    [InlineData(0, 180.0001, false)]
    [InlineData(0, -180.0001, false)]
    public void Driver_location_must_be_a_real_position_UC_CUS_13(double lat, double lng, bool valid)
    {
        var result = new UpdateDriverLocationCommandValidator().TestValidate(new UpdateDriverLocationCommand(1, (decimal)lat, (decimal)lng));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Tracking_snapshot_needs_a_real_order_id_UC_CUS_13(long orderId, bool valid)
    {
        var result = new GetOrderTrackingQueryValidator().TestValidate(new GetOrderTrackingQuery(orderId));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor(x => x.OrderId);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    public void Follow_check_needs_real_user_and_order_ids_B12(long userId, long orderId, bool valid)
    {
        var result = new CanFollowOrderQueryValidator().TestValidate(new CanFollowOrderQuery(userId, orderId));

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }
}
