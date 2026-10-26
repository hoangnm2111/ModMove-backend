using ModMove.Domain.Entities;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

/// <summary>
/// Unit tests for Master Data, Pricing, and Coupon Domain entities.
/// Covers happy cases and edge cases (GB-21, B11, B13, DECISIONS C8).
/// </summary>
public class AdminManagementDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

    // ==========================================
    // 1. VehicleType Fare Update & Status Tests
    // ==========================================

    [Fact]
    public void VehicleType_update_fares_happy_path()
    {
        var type = VehicleType.Create("Xe tải 500kg", 500, 170, 120, 120, 50_000, 10_000, 80_000);

        type.UpdateFares(60_000, 12_000, 90_000);

        Assert.Equal(60_000m, type.BaseFare);
        Assert.Equal(12_000m, type.PricePerKm);
        Assert.Equal(90_000m, type.MinimumFare);
    }

    [Fact]
    public void VehicleType_set_active_toggles_state()
    {
        var type = VehicleType.Create("Xe tải 500kg", 500, 170, 120, 120, 50_000, 10_000, 80_000);
        Assert.True(type.IsActive);

        type.SetActive(false);
        Assert.False(type.IsActive);

        type.SetActive(true);
        Assert.True(type.IsActive);
    }

    // ==========================================
    // 2. MasterDataItem Tests (DECISIONS B13)
    // ==========================================

    [Fact]
    public void MasterDataItem_create_and_update_content_happy_path()
    {
        var item = MasterDataItem.Create("POLICY", "TERMS", "Điều khoản sử dụng", "Nội dung ban đầu");
        Assert.Equal("POLICY", item.Category);
        Assert.Equal("TERMS", item.Code);
        Assert.Equal("Điều khoản sử dụng", item.Name);
        Assert.Equal("Nội dung ban đầu", item.Description);
        Assert.True(item.IsActive);

        item.Update("Điều khoản sửa đổi", "Nội dung cập nhật mới 2026");
        Assert.Equal("Điều khoản sửa đổi", item.Name);
        Assert.Equal("Nội dung cập nhật mới 2026", item.Description);

        item.UpdateDescription("Chỉ cập nhật mô tả");
        Assert.Equal("Chỉ cập nhật mô tả", item.Description);

        item.SetActive(false);
        Assert.False(item.IsActive);
    }

    // ==========================================
    // 3. Coupon Domain Logic Tests (GB-21, B11)
    // ==========================================

    [Fact]
    public void Coupon_ensure_applicable_happy_path()
    {
        var coupon = Coupon.Create("DISCOUNT20", 20_000, 100_000, Now.AddDays(-1), Now.AddDays(7), 50);

        // Valid order value >= MinimumOrderValue, within time window
        coupon.EnsureApplicable(orderValue: 150_000, Now);

        Assert.Equal(20_000m, coupon.DiscountFor(150_000));
    }

    [Fact]
    public void Coupon_discount_never_exceeds_order_value()
    {
        var coupon = Coupon.Create("SUPER50", 50_000, 0, Now.AddDays(-1), Now.AddDays(7), null);

        // Edge case: order value is less than discount value
        Assert.Equal(30_000m, coupon.DiscountFor(30_000));
    }

    [Fact]
    public void Coupon_ensure_applicable_when_inactive_throws_DomainException()
    {
        var coupon = Coupon.Create("INACTIVE", 20_000, 100_000, Now.AddDays(-1), Now.AddDays(7), 50);
        coupon.SetActive(false);

        var ex = Assert.Throws<DomainException>(() => coupon.EnsureApplicable(150_000, Now));
        Assert.Equal("The coupon is not valid at this time.", ex.Message);
    }

    [Fact]
    public void Coupon_ensure_applicable_outside_time_window_throws_DomainException()
    {
        var coupon = Coupon.Create("TIMED", 20_000, 100_000, Now.AddDays(1), Now.AddDays(7), 50);

        // Edge case: before ValidFrom
        var exBefore = Assert.Throws<DomainException>(() => coupon.EnsureApplicable(150_000, Now));
        Assert.Equal("The coupon is not valid at this time.", exBefore.Message);

        // Edge case: after ExpiresAt
        var exAfter = Assert.Throws<DomainException>(() => coupon.EnsureApplicable(150_000, Now.AddDays(8)));
        Assert.Equal("The coupon is not valid at this time.", exAfter.Message);
    }

    [Fact]
    public void Coupon_ensure_applicable_under_minimum_order_value_throws_DomainException()
    {
        var coupon = Coupon.Create("MIN100", 20_000, 100_000, Now.AddDays(-1), Now.AddDays(7), 50);

        // Edge case: order value < 100,000
        var ex = Assert.Throws<DomainException>(() => coupon.EnsureApplicable(99_999, Now));
        Assert.Equal("The order does not reach the minimum value of the coupon.", ex.Message);
    }

    [Fact]
    public void Coupon_ensure_applicable_when_usage_limit_reached_throws_DomainException_B11()
    {
        var coupon = Coupon.Create("LIMITED", 20_000, 100_000, Now.AddDays(-1), Now.AddDays(7), usageLimit: 2);

        coupon.MarkUsed();
        coupon.MarkUsed();
        Assert.Equal(2, coupon.UsedCount);

        // Edge case: limit reached
        var ex = Assert.Throws<DomainException>(() => coupon.EnsureApplicable(150_000, Now));
        Assert.Equal("The coupon has reached its usage limit.", ex.Message);
    }

    // ==========================================
    // 4. PlatformSetting Tests (DECISIONS C8)
    // ==========================================

    [Fact]
    public void PlatformSetting_change_updates_value_and_audit_fields()
    {
        var setting = PlatformSetting.Create("COMMISSION_RATE", "0.10", Now);

        setting.Change("0.15", Now.AddHours(1), adminId: 99);

        Assert.Equal("0.15", setting.SettingValue);
        Assert.Equal(Now.AddHours(1), setting.EffectiveFrom);
        Assert.Equal(99, setting.UpdatedBy);
    }
}
