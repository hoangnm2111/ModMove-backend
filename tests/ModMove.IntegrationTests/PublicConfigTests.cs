using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Catalog.Queries.GetPublicConfig;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;

namespace ModMove.IntegrationTests;

/// <summary>GET api/public/config: the numbers the web and the apps show, read from the Administrator's settings and BusinessRules.</summary>
public class PublicConfigTests
{
    private static TestApp App(string loadingFee = "200000", string deposit = "200000", string radius = "5")
    {
        var app = new TestApp();
        app.Settings.Add(PlatformSetting.Create(SettingKeys.LoadingFee, loadingFee, TestApp.Start));
        app.Settings.Add(PlatformSetting.Create(SettingKeys.RequiredDeposit, deposit, TestApp.Start));
        app.Settings.Add(PlatformSetting.Create(SettingKeys.MatchingRadiusKm, radius, TestApp.Start));
        return app;
    }

    [Fact]
    public async Task Money_and_radius_come_from_the_administrator_settings_not_from_code()
    {
        var config = await App(loadingFee: "250000", deposit: "300000", radius: "7").Sender.Send(new GetPublicConfigQuery());

        Assert.Equal(250_000m, config.LoadingFee);
        Assert.Equal(300_000m, config.RequiredDeposit);
        Assert.Equal(7m, config.MatchingRadiusKm);
    }

    [Fact]
    public async Task Works_without_signing_in()
    {
        // TestApp starts signed out: the query must not need a user.
        var config = await App().Sender.Send(new GetPublicConfigQuery());

        Assert.Equal(200_000m, config.LoadingFee);
    }

    [Fact]
    public async Task Rules_are_the_fixed_business_limits_in_whole_minutes_and_hours()
    {
        var rules = (await App().Sender.Send(new GetPublicConfigQuery())).Rules;

        Assert.Equal((BusinessRules.MaxCargoPhotos, BusinessRules.MaxPhotoSizeMb, BusinessRules.MaxOrderNoteLength), (rules.MaxCargoPhotos, rules.MaxPhotoSizeMb, rules.MaxOrderNoteLength));
        Assert.Equal((5, 5, 500), (rules.MaxCargoPhotos, rules.MaxPhotoSizeMb, rules.MaxOrderNoteLength));
        Assert.Equal((10, 30, 10), (rules.MatchingTimeoutMinutes, rules.UnpaidPaymentMinutes, rules.PayOsCodeMinutes));
        Assert.Equal((24, 24, 24), (rules.ComplaintWindowHours, rules.RatingWindowHours, rules.AutoCompleteHours));
        Assert.Equal((2, 5, 5), (rules.MinPickupPhotos, rules.MaxPickupSubmissions, rules.MaxEvidencePhotos));
        Assert.Equal(500, rules.ArrivalRadiusMeters);
        Assert.Equal((8, 20), (rules.PasswordMinLength, rules.PasswordMaxLength));
        Assert.Equal((5, 5, 10), (rules.OtpLifetimeMinutes, rules.MaxFailedLogins, rules.LoginLockMinutes));
    }

    [Fact]
    public async Task Never_exposes_the_commission_rate()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(await App().Sender.Send(new GetPublicConfigQuery()));

        Assert.DoesNotContain("ommission", json);
    }

    [Fact]
    public void Endpoint_is_anonymous_and_cached_briefly()
    {
        var get = typeof(ModMove.WebApi.Controllers.Public.ConfigController).GetMethod(nameof(ModMove.WebApi.Controllers.Public.ConfigController.Get))!;

        Assert.Equal(60, ((Microsoft.AspNetCore.Mvc.ResponseCacheAttribute)get.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ResponseCacheAttribute), false).Single()).Duration);
        Assert.Empty(get.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true));
    }
}
