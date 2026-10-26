using FluentValidation.TestHelper;
using ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;
using ModMove.Application.Features.MasterData.Commands.CreateMasterDataItem;
using ModMove.Application.Features.MasterData.Coupons.Commands.CreateCoupon;
using ModMove.Application.Features.Pricing.Commands.UpdatePlatformSettings;
using ModMove.Application.Features.Pricing.Commands.UpdateVehicleTypeFares;

namespace ModMove.UnitTests.Application;

/// <summary>
/// Unit tests for Admin CQRS Command Validators (Happy cases and Edge cases).
/// </summary>
public class AdminCommandValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

    // ==========================================
    // 1. UpdateVehicleTypeFaresCommandValidator
    // ==========================================

    [Fact]
    public void UpdateVehicleTypeFares_validator_happy_path()
    {
        var validator = new UpdateVehicleTypeFaresCommandValidator();
        var command = new UpdateVehicleTypeFaresCommand(1, 50_000, 10_000, 80_000);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, 50_000, 10_000, 80_000, nameof(UpdateVehicleTypeFaresCommand.Id))]
    [InlineData(-1, 50_000, 10_000, 80_000, nameof(UpdateVehicleTypeFaresCommand.Id))]
    [InlineData(1, -1, 10_000, 80_000, nameof(UpdateVehicleTypeFaresCommand.BaseFare))]
    [InlineData(1, 50_000, 0, 80_000, nameof(UpdateVehicleTypeFaresCommand.PricePerKm))]
    [InlineData(1, 50_000, -100, 80_000, nameof(UpdateVehicleTypeFaresCommand.PricePerKm))]
    [InlineData(1, 50_000, 10_000, -1, nameof(UpdateVehicleTypeFaresCommand.MinimumFare))]
    public void UpdateVehicleTypeFares_validator_edge_cases_have_errors(
        long id, decimal baseFare, decimal pricePerKm, decimal minFare, string expectedErrorField)
    {
        var validator = new UpdateVehicleTypeFaresCommandValidator();
        var command = new UpdateVehicleTypeFaresCommand(id, baseFare, pricePerKm, minFare);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(expectedErrorField);
    }

    // ==========================================
    // 2. UpdatePlatformSettingsCommandValidator
    // ==========================================

    [Fact]
    public void UpdatePlatformSettings_validator_happy_path()
    {
        var validator = new UpdatePlatformSettingsCommandValidator();
        var command = new UpdatePlatformSettingsCommand(0.15m, 100_000m, 200_000m, 5m);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdatePlatformSettings_validator_all_null_fails()
    {
        var validator = new UpdatePlatformSettingsCommandValidator();
        var command = new UpdatePlatformSettingsCommand(null, null, null, null);

        var result = validator.TestValidate(command);

        // Edge case: must provide at least one setting
        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void UpdatePlatformSettings_invalid_commission_rate_fails(decimal rate)
    {
        var validator = new UpdatePlatformSettingsCommandValidator();
        var command = new UpdatePlatformSettingsCommand(rate, null, null, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CommissionRate!.Value);
    }

    [Theory]
    [InlineData(-1, 0, 5)]
    [InlineData(0, -1, 5)]
    [InlineData(0, 0, 0)] // Radius must be > 0
    [InlineData(0, 0, -5)]
    public void UpdatePlatformSettings_invalid_fee_deposit_or_radius_fails(
        decimal loadingFee, decimal minDeposit, decimal radius)
    {
        var validator = new UpdatePlatformSettingsCommandValidator();
        var command = new UpdatePlatformSettingsCommand(null, loadingFee, minDeposit, radius);

        var result = validator.TestValidate(command);

        Assert.True(result.Errors.Count > 0);
    }

    // ==========================================
    // 3. CreateMasterDataItemCommandValidator
    // ==========================================

    [Fact]
    public void CreateMasterDataItem_validator_happy_path()
    {
        var validator = new CreateMasterDataItemCommandValidator();
        var command = new CreateMasterDataItemCommand("POLICY", "TERMS", "Điều khoản", "Chi tiết...");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "CODE", "Name", nameof(CreateMasterDataItemCommand.Category))]
    [InlineData("CATEGORY", "", "Name", nameof(CreateMasterDataItemCommand.Code))]
    [InlineData("CATEGORY", "CODE", "", nameof(CreateMasterDataItemCommand.Name))]
    public void CreateMasterDataItem_empty_fields_fail(
        string category, string code, string name, string expectedErrorField)
    {
        var validator = new CreateMasterDataItemCommandValidator();
        var command = new CreateMasterDataItemCommand(category, code, name, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(expectedErrorField);
    }

    // ==========================================
    // 4. CreateCouponCommandValidator
    // ==========================================

    [Fact]
    public void CreateCoupon_validator_happy_path()
    {
        var validator = new CreateCouponCommandValidator();
        var command = new CreateCouponCommand(
            "CHAOBANMOI", 20_000m, 100_000m, Now, Now.AddDays(30), 100);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", 20_000, 100_000, nameof(CreateCouponCommand.DiscountCode))] // Empty code
    [InlineData("CODE WITH SPACES", 20_000, 100_000, nameof(CreateCouponCommand.DiscountCode))] // Space not allowed
    [InlineData("CODE!@#", 20_000, 100_000, nameof(CreateCouponCommand.DiscountCode))] // Special chars
    [InlineData("VALIDCODE", 0, 100_000, nameof(CreateCouponCommand.DiscountValue))] // Discount <= 0
    [InlineData("VALIDCODE", -5000, 100_000, nameof(CreateCouponCommand.DiscountValue))]
    [InlineData("VALIDCODE", 20_000, -1, nameof(CreateCouponCommand.MinimumOrderValue))] // Negative min order
    public void CreateCoupon_invalid_code_or_values_fail(
        string code, decimal discount, decimal minOrder, string expectedErrorField)
    {
        var validator = new CreateCouponCommandValidator();
        var command = new CreateCouponCommand(code, discount, minOrder, Now, Now.AddDays(7), 10);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(expectedErrorField);
    }

    [Fact]
    public void CreateCoupon_expiration_before_or_equal_to_valid_from_fails()
    {
        var validator = new CreateCouponCommandValidator();

        // Edge case: ExpiresAt == ValidFrom
        var commandEqual = new CreateCouponCommand("CODE", 20_000m, 0, Now, Now, 10);
        validator.TestValidate(commandEqual).ShouldHaveValidationErrorFor(x => x.ExpiresAt);

        // Edge case: ExpiresAt < ValidFrom
        var commandPast = new CreateCouponCommand("CODE", 20_000m, 0, Now, Now.AddMinutes(-1), 10);
        validator.TestValidate(commandPast).ShouldHaveValidationErrorFor(x => x.ExpiresAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CreateCoupon_non_positive_usage_limit_fails(int limit)
    {
        var validator = new CreateCouponCommandValidator();
        var command = new CreateCouponCommand("CODE", 20_000m, 0, Now, Now.AddDays(7), limit);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UsageLimit);
    }

    // ==========================================
    // 5. RejectDriverApplicationCommandValidator
    // ==========================================

    [Fact]
    public void RejectDriverApplication_validator_happy_path()
    {
        var validator = new RejectDriverApplicationCommandValidator();
        var command = new RejectDriverApplicationCommand(1, ["Ảnh giấy tờ bị mờ", "Biển số xe không rõ"]);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RejectDriverApplication_empty_reasons_list_fails()
    {
        var validator = new RejectDriverApplicationCommandValidator();
        var command = new RejectDriverApplicationCommand(1, []);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Reasons);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectDriverApplication_empty_or_whitespace_reason_item_fails(string emptyReason)
    {
        var validator = new RejectDriverApplicationCommandValidator();
        var command = new RejectDriverApplicationCommand(1, ["Lý do hợp lệ", emptyReason]);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Reasons);
    }
}
