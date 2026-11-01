using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Orders.Commands.EstimateFare;
using ModMove.Domain.Entities;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>POST api/public/fareestimate (B1, GB-10, GB-21) through the real pipeline with fakes.</summary>
public class FareEstimateTests
{
    private static TestApp App()
    {
        var app = new TestApp();
        // Seeded "Xe van 500kg" (bed 170 × 120 × 120 cm), LOADING_FEE 100,000 and WELCOME20K.
        app.VehicleTypes.Add(VehicleType.Create("Xe van 500kg", 500, 170, 120, 120, 50_000, 10_000, 80_000));
        app.Settings.Add(PlatformSetting.Create(SettingKeys.LoadingFee, "100000", TestApp.Start));
        app.Coupons.Add(Coupon.Create("WELCOME20K", 20_000, 100_000, TestApp.Start.AddDays(-1), TestApp.Start.AddYears(1), 1_000));
        app.Maps.Route = new RouteInfo(12m, 30);
        return app;
    }

    private static EstimateFareCommand Estimate(
        decimal weightKg = 100, int length = 100, int width = 100, int height = 100,
        bool loading = true, string? coupon = "WELCOME20K", string pickup = "Hoàn Kiếm, Hà Nội") =>
        new(1, pickup, "Cầu Giấy, Hà Nội", weightKg, length, width, height, loading, coupon);

    [Fact]
    public async Task Estimate_uses_google_km_and_B1()
    {
        var result = await App().Sender.Send(Estimate());

        Assert.Equal(new FareEstimateResponse(12m, 30, 170_000, 100_000, 20_000, 250_000), result);
    }

    [Fact]
    public async Task Coupon_code_is_matched_case_insensitively()
    {
        var result = await App().Sender.Send(Estimate(coupon: " welcome20k "));

        Assert.Equal(20_000m, result.DiscountAmount);
    }

    [Fact]
    public async Task Cargo_too_heavy_is_VEHICLE_TOO_SMALL_GB10()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => App().Sender.Send(Estimate(weightKg: 501)));

        Assert.Equal("VEHICLE_TOO_SMALL", error.Code);
    }

    [Fact]
    public async Task Turned_item_fits_4_2()
    {
        var result = await App().Sender.Send(Estimate(length: 100, width: 160, height: 110));

        Assert.Equal(250_000m, result.TotalFee);
    }

    [Fact]
    public async Task Unknown_coupon_is_COUPON_NOT_FOUND()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => App().Sender.Send(Estimate(coupon: "NOPE")));

        Assert.Equal("COUPON_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Unknown_address_is_ADDRESS_NOT_FOUND()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => App().Sender.Send(Estimate(pickup: FakeMapService.UnknownAddress)));

        Assert.Equal("ADDRESS_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Inactive_vehicle_type_is_not_found()
    {
        var app = App();
        app.VehicleTypes.Items[0].SetActive(false);

        await Assert.ThrowsAsync<NotFoundException>(() => app.Sender.Send(Estimate()));
    }

    [Fact]
    public async Task Estimate_does_not_save_or_use_up_the_coupon()
    {
        var app = App();

        await app.Sender.Send(Estimate());

        Assert.Equal(0, app.UnitOfWork.Saves);
        Assert.Equal(0, app.Coupons.Items[0].UsedCount);
    }

    [Fact]
    public async Task Zero_weight_is_rejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() => App().Sender.Send(Estimate(weightKg: 0)));
    }
}
