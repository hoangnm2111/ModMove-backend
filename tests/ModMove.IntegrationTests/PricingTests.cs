using System.Data;
using System.Globalization;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Pricing.Commands.UpdatePlatformSettings;
using ModMove.Application.Features.Pricing.Commands.UpdateVehicleTypeFares;
using ModMove.Application.Features.Pricing.Queries.GetPricingConfigurations;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

internal sealed class InMemoryPlatformSettings : IPlatformSettingRepository, IPlatformSettings
{
    public List<PlatformSetting> Items { get; } = [];

    public Task<IReadOnlyList<PlatformSetting>> GetByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlatformSetting>>(Items.Where(s => keys.Contains(s.SettingKey)).ToList());

    // The query side reads the same rows, parsed like production (invariant culture).
    public Task<decimal> GetDecimalAsync(string key, CancellationToken cancellationToken)
    {
        var item = Items.SingleOrDefault(s => s.SettingKey == key);
        if (item is null && key == SettingKeys.RequiredDeposit)
            return Task.FromResult(200_000m);

        return Task.FromResult(decimal.Parse(Items.Single(s => s.SettingKey == key).SettingValue, NumberStyles.Number, CultureInfo.InvariantCulture));
    }

    public Task<PlatformSetting?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(s => s.Id == id));

    public void Add(PlatformSetting aggregate) => Items.Add(aggregate);
    public void Update(PlatformSetting aggregate) { }
    public void Remove(PlatformSetting aggregate) => Items.Remove(aggregate);
}

/// <summary>Screen #35: UC-ADM-10, UC-ADM-11, UC-ADM-13 and DECISIONS C8 through the real pipeline with fakes.</summary>
public class PricingTests
{
    private sealed class PricingApp
    {
        public User Admin { get; } = User.Create("Admin", "0900000009", "admin@x.vn", "h:x", UserRole.Administrator, emailVerified: true);
        public FakeSql Sql { get; } = new();
        public InMemoryRepository<VehicleType> VehicleTypes { get; } = new();
        public InMemoryPlatformSettings Settings { get; } = new();
        public InMemoryOrders Orders { get; } = new();
        public CountingUnitOfWork UnitOfWork { get; } = new();
        public MutableTime Time { get; } = new(TestApp.Start);
        public ISender Sender { get; }

        public PricingApp(bool signedIn = true)
        {
            var users = new InMemoryUsers(Admin); // gets id 1

            var vanType = VehicleType.Create("Xe van 500kg", 500, 170, 120, 120, 50_000, 10_000, 80_000);
            TestIds.Set(vanType, 1);
            VehicleTypes.Add(vanType);

            // Same values the seed writes.
            foreach (var (key, value) in new[] { ("COMMISSION_RATE", "0.10"), ("LOADING_FEE", "100000"), ("REQUIRED_DEPOSIT", "200000"), ("MATCHING_RADIUS_KM", "5") })
                Settings.Add(PlatformSetting.Create(key, value, TestApp.Start));

            Sender = new ServiceCollection()
                .AddLogging()
                .AddApplication()
                .AddSingleton<ISqlConnectionFactory>(Sql)
                .AddSingleton<IUserRepository>(users)
                .AddSingleton<IRepository<VehicleType>>(VehicleTypes)
                .AddSingleton<IPlatformSettingRepository>(Settings)
                .AddSingleton<IPlatformSettings>(Settings)
                .AddSingleton<IShippingOrderRepository>(Orders)
                .AddSingleton<IUnitOfWork>(UnitOfWork)
                .AddSingleton<ICurrentUserService>(new FakeCurrentUser(signedIn ? Admin.Id : null))
                .AddSingleton<TimeProvider>(Time)
                .BuildServiceProvider()
                .GetRequiredService<ISender>();
        }

        public string Setting(string key) => Settings.Items.Single(s => s.SettingKey == key).SettingValue;
    }

    // ---- UC-ADM-10: view ----

    [Fact]
    public async Task Admin_sees_every_vehicle_type_and_the_four_settings()
    {
        var app = new PricingApp();
        var rows = new DataTable();
        rows.Columns.Add("Id", typeof(long));
        rows.Columns.Add("Name", typeof(string));
        rows.Columns.Add("MaxLoadWeightKg", typeof(decimal));
        rows.Columns.Add("BaseFare", typeof(decimal));
        rows.Columns.Add("PricePerKm", typeof(decimal));
        rows.Columns.Add("MinimumFare", typeof(decimal));
        rows.Columns.Add("IsActive", typeof(bool));
        rows.Rows.Add(1L, "Xe van 500kg", 500m, 50_000m, 10_000m, 80_000m, true);
        rows.Rows.Add(2L, "Xe tải 2 tấn", 2_000m, 130_000m, 20_000m, 260_000m, false);
        app.Sql.Result = rows;

        var response = await app.Sender.Send(new GetPricingConfigurationsQuery());

        Assert.Equal(2, response.VehicleTypes.Count);
        Assert.False(response.VehicleTypes[1].IsActive); // a disabled type still shows, so it can be re-enabled
        Assert.Equal(0.10m, response.Settings.CommissionRate);
        Assert.Equal(100_000m, response.Settings.LoadingFee);
        Assert.Equal(200_000m, response.Settings.RequiredDeposit);
        Assert.Equal(5m, response.Settings.MatchingRadiusKm);
    }

    [Fact]
    public async Task Pricing_query_does_not_hide_inactive_types()
    {
        var app = new PricingApp();
        app.Sql.Result = new DataTable();
        app.Sql.Result.Columns.Add("Id", typeof(long));
        app.Sql.Result.Columns.Add("Name", typeof(string));
        app.Sql.Result.Columns.Add("MaxLoadWeightKg", typeof(decimal));
        app.Sql.Result.Columns.Add("BaseFare", typeof(decimal));
        app.Sql.Result.Columns.Add("PricePerKm", typeof(decimal));
        app.Sql.Result.Columns.Add("MinimumFare", typeof(decimal));
        app.Sql.Result.Columns.Add("IsActive", typeof(bool));

        await app.Sender.Send(new GetPricingConfigurationsQuery());

        Assert.Contains("FROM vehicle_types", app.Sql.LastSql);
        Assert.DoesNotContain("WHERE", app.Sql.LastSql);
        Assert.Contains("is_active AS IsActive", app.Sql.LastSql);
    }

    // ---- UC-ADM-11: vehicle type fares ----

    [Fact]
    public async Task Fares_of_a_vehicle_type_are_updated_and_saved()
    {
        var app = new PricingApp();

        await app.Sender.Send(new UpdateVehicleTypeFaresCommand(1, 60_000m, 12_000m, 90_000m));

        var type = app.VehicleTypes.Items.Single();
        Assert.Equal(60_000m, type.BaseFare);
        Assert.Equal(12_000m, type.PricePerKm);
        Assert.Equal(90_000m, type.MinimumFare);
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Unknown_vehicle_type_is_not_found()
    {
        var app = new PricingApp();

        await Assert.ThrowsAsync<NotFoundException>(() => app.Sender.Send(new UpdateVehicleTypeFaresCommand(99, 60_000m, 12_000m, 90_000m)));
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Theory]
    [InlineData(-1, 12_000, 90_000)]       // negative base fare
    [InlineData(60_000, 0, 90_000)]        // a price per km of 0 would make every route cost only the base fare
    [InlineData(60_000, -5, 90_000)]
    [InlineData(60_000, 12_000, -1)]       // negative minimum fare
    [InlineData(2_000_000_000, 12_000, 90_000)] // would overflow DECIMAL(15,2) rules of the table: refused as 400
    public async Task Invalid_fares_are_refused_before_anything_changes(decimal baseFare, decimal perKm, decimal minimum)
    {
        var app = new PricingApp();

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(new UpdateVehicleTypeFaresCommand(1, baseFare, perKm, minimum)));

        Assert.Equal(50_000m, app.VehicleTypes.Items.Single().BaseFare);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task A_signed_out_caller_cannot_change_fares()
    {
        var app = new PricingApp(signedIn: false);

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new UpdateVehicleTypeFaresCommand(1, 60_000m, 12_000m, 90_000m)));
    }

    // ---- UC-ADM-13 / DECISIONS C8: platform settings ----

    [Fact]
    public async Task Several_settings_change_in_one_call_and_are_stored_as_invariant_text()
    {
        var app = new PricingApp();
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("vi-VN"); // ',' decimal separator: "0.15" must still be written as "0.15"
        try
        {
            await app.Sender.Send(new UpdatePlatformSettingsCommand(0.15m, 150_000m, null, 7.5m));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.Equal("0.15", app.Setting("COMMISSION_RATE"));
        Assert.Equal("150000", app.Setting("LOADING_FEE"));
        Assert.Equal("7.5", app.Setting("MATCHING_RADIUS_KM"));
        Assert.Equal("200000", app.Setting("REQUIRED_DEPOSIT")); // null = untouched
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Change_records_who_and_from_when_UC_ADM_13()
    {
        var app = new PricingApp();
        app.Time.Now = TestApp.Start.AddDays(3);

        await app.Sender.Send(new UpdatePlatformSettingsCommand(0.12m, null, null, null));

        var row = app.Settings.Items.Single(s => s.SettingKey == "COMMISSION_RATE");
        Assert.Equal(app.Admin.Id, row.UpdatedBy);
        Assert.Equal(TestApp.Start.AddDays(3), row.EffectiveFrom);
    }

    [Fact]
    public async Task Commission_rate_is_locked_while_orders_are_in_progress_C8()
    {
        var app = new PricingApp();
        app.Orders.HasOrdersInProgress = true;

        var error = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new UpdatePlatformSettingsCommand(0.20m, null, null, null)));

        Assert.Equal("COMMISSION_RATE_LOCKED", error.Code);
        Assert.Equal("0.10", app.Setting("COMMISSION_RATE"));
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task A_locked_commission_rate_blocks_the_whole_call_so_nothing_is_half_applied()
    {
        var app = new PricingApp();
        app.Orders.HasOrdersInProgress = true;

        await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new UpdatePlatformSettingsCommand(0.20m, 150_000m, null, null)));

        Assert.Equal("100000", app.Setting("LOADING_FEE"));
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Other_settings_can_change_while_orders_are_in_progress()
    {
        var app = new PricingApp();
        app.Orders.HasOrdersInProgress = true;

        await app.Sender.Send(new UpdatePlatformSettingsCommand(null, 150_000m, 250_000m, 8m));

        Assert.Equal("150000", app.Setting("LOADING_FEE"));
        Assert.Equal("250000", app.Setting("REQUIRED_DEPOSIT"));
        Assert.Equal("8", app.Setting("MATCHING_RADIUS_KM"));
    }

    [Fact]
    public async Task Sending_the_current_commission_rate_again_is_not_a_change_and_is_not_blocked()
    {
        var app = new PricingApp();
        app.Orders.HasOrdersInProgress = true;

        await app.Sender.Send(new UpdatePlatformSettingsCommand(0.10m, null, null, null)); // stored "0.10" == 0.10

        Assert.Equal("0.10", app.Setting("COMMISSION_RATE"));
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Commission_rate_changes_when_no_order_is_running()
    {
        var app = new PricingApp();

        await app.Sender.Send(new UpdatePlatformSettingsCommand(0.20m, null, null, null));

        Assert.Equal("0.20", app.Setting("COMMISSION_RATE"));
    }

    [Fact]
    public async Task A_missing_setting_row_is_a_setup_error_not_a_user_error()
    {
        var app = new PricingApp();
        app.Settings.Items.RemoveAll(s => s.SettingKey == "LOADING_FEE");

        await Assert.ThrowsAsync<InvalidOperationException>(() => app.Sender.Send(new UpdatePlatformSettingsCommand(null, 150_000m, null, null)));
    }

    [Theory]
    [InlineData(null, null, null, null)] // nothing to change
    [InlineData(-0.01, null, null, null)]
    [InlineData(1.01, null, null, null)] // a rate is a fraction, 10 would mean 1000%
    [InlineData(null, -1.0, null, null)]
    [InlineData(null, null, -1.0, null)]
    [InlineData(null, null, null, 0.0)]
    [InlineData(null, null, null, -3.0)]
    public async Task Invalid_settings_are_refused_before_anything_changes(double? rate, double? loading, double? deposit, double? radius)
    {
        var app = new PricingApp();
        var command = new UpdatePlatformSettingsCommand((decimal?)rate, (decimal?)loading, (decimal?)deposit, (decimal?)radius);

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(command));

        Assert.Equal(0, app.UnitOfWork.Saves);
    }
}
