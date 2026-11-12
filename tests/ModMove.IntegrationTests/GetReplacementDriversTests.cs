using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Incidents.Queries.GetReplacementDrivers;
using ModMove.Domain.Entities;

namespace ModMove.IntegrationTests;

/// <summary>
/// GET api/dispatcher/incidents/{id}/replacement-drivers (UC-DIS-06, DECISIONS B5, 6.4, 7.2, GB-16)
/// through the real MediatR pipeline with <see cref="FakeSql"/>.
/// </summary>
public class GetReplacementDriversTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private static ISender SenderWith(FakeSql sql)
    {
        var settings = new InMemoryPlatformSettings();
        settings.Add(PlatformSetting.Create(SettingKeys.MatchingRadiusKm, "5", Now));
        settings.Add(PlatformSetting.Create(SettingKeys.RequiredDeposit, "200000", Now));
        settings.Add(PlatformSetting.Create(SettingKeys.CommissionRate, "0.10", Now));

        return new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(sql)
            .AddSingleton<IPlatformSettings>(settings)
            .AddSingleton<TimeProvider>(new MutableTime(Now))
            .BuildServiceProvider()
            .GetRequiredService<ISender>();
    }

    private static DataTable IncidentContextTable()
    {
        var t = new DataTable();
        t.Columns.Add("IncidentId", typeof(long));
        t.Columns.Add("IncidentLatitude", typeof(decimal));
        t.Columns.Add("IncidentLongitude", typeof(decimal));
        t.Columns.Add("OrderId", typeof(long));
        t.Columns.Add("CurrentDriverProfileId", typeof(long));
        t.Columns.Add("VehicleTypeId", typeof(long));
        t.Columns.Add("PickupLatitude", typeof(decimal));
        t.Columns.Add("PickupLongitude", typeof(decimal));
        t.Columns.Add("TotalFee", typeof(decimal));
        t.Columns.Add("PaymentMethod", typeof(string));
        return t;
    }

    private static DataTable CandidateDriversTable()
    {
        var t = new DataTable();
        t.Columns.Add("DriverProfileId", typeof(long));
        t.Columns.Add("UserId", typeof(long));
        t.Columns.Add("FullName", typeof(string));
        t.Columns.Add("PhoneNumber", typeof(string));
        t.Columns.Add("LicensePlate", typeof(string));
        t.Columns.Add("VehicleTypeId", typeof(long));
        t.Columns.Add("VehicleTypeName", typeof(string));
        t.Columns.Add("LastLatitude", typeof(decimal));
        t.Columns.Add("LastLongitude", typeof(decimal));
        t.Columns.Add("LastLocationAt", typeof(DateTime));
        t.Columns.Add("WalletStatus", typeof(string));
        t.Columns.Add("DepositBalance", typeof(decimal));
        return t;
    }

    [Fact]
    public async Task FiltersByRadiusAndCodDeposit_AndSortsNearestFirst()
    {
        var sql = new FakeSql();

        var ctx = IncidentContextTable();
        // Cash order with TotalFee = 2,500,000 => 10% commission = 250,000 (> 200,000 MinCodDeposit!)
        ctx.Rows.Add(10L, 21.0285m, 105.8542m, 100L, 1L, 1L, 21.0000m, 105.8000m, 2_500_000m, "Cash");
        sql.Results.Enqueue(ctx);

        var candidates = CandidateDriversTable();
        // Driver 2: ~1.5 km away, deposit 300k => eligible
        candidates.Rows.Add(2L, 20L, "Tài Xa Hơn", "0900000002", "29A-00002", 1L, "Xe tải 500kg", 21.0400m, 105.8600m, Now.UtcDateTime, "Active", 300_000m);
        // Driver 3: ~0.3 km away, deposit 260k => eligible & nearest!
        candidates.Rows.Add(3L, 30L, "Tài Gần Nhất", "0900000003", "29A-00003", 1L, "Xe tải 500kg", 21.0300m, 105.8550m, Now.UtcDateTime, "Active", 260_000m);
        // Driver 4: ~0.5 km away, deposit 220k (< 250k commission of this order, GB-16) => excluded!
        candidates.Rows.Add(4L, 40L, "Tài Thiếu Cọc", "0900000004", "29A-00004", 1L, "Xe tải 500kg", 21.0310m, 105.8560m, Now.UtcDateTime, "Active", 220_000m);
        // Driver 5: ~15 km away (> 5 km radius) => excluded!
        candidates.Rows.Add(5L, 50L, "Tài Quá Xa", "0900000005", "29A-00005", 1L, "Xe tải 500kg", 21.1500m, 105.9500m, Now.UtcDateTime, "Active", 500_000m);
        sql.Results.Enqueue(candidates);

        var result = await SenderWith(sql).Send(new GetReplacementDriversQuery(10));

        Assert.Equal(2, result.Count);
        Assert.Equal(3L, result[0].DriverProfileId);
        Assert.Equal(2L, result[1].DriverProfileId);
        Assert.True(result[0].DistanceKm < result[1].DistanceKm);
    }

    [Fact]
    public async Task Incident_with_one_coordinate_falls_back_to_the_pickup_point()
    {
        var sql = new FakeSql();

        var ctx = IncidentContextTable();
        // Older incident saved with a latitude only: the pickup point in Ha Noi must be used, not (incident lat, pickup lng).
        ctx.Rows.Add(10L, 10.7769m, DBNull.Value, 100L, 1L, 1L, 21.0285m, 105.8542m, 500_000m, "PayOS");
        sql.Results.Enqueue(ctx);

        var candidates = CandidateDriversTable();
        candidates.Rows.Add(2L, 20L, "Tài Ở Điểm Lấy", "0900000002", "29A-00002", 1L, "Xe tải 500kg", 21.0290m, 105.8545m, Now.UtcDateTime, "Active", 200_000m);
        sql.Results.Enqueue(candidates);

        var result = await SenderWith(sql).Send(new GetReplacementDriversQuery(10));

        Assert.Equal(2L, Assert.Single(result).DriverProfileId);
    }

    [Theory]
    [InlineData("Customer")]   // the point is the customer's phone
    [InlineData("Dispatcher")] // a Dispatcher's incident has no point at all (Q6b)
    public async Task Incident_not_reported_by_a_driver_measures_from_the_current_drivers_position_7_2(string reporterRole)
    {
        var sql = new FakeSql();

        var ctx = IncidentContextTable();
        ctx.Columns.Add("ReporterRole", typeof(string));
        ctx.Columns.Add("DriverLatitude", typeof(decimal));
        ctx.Columns.Add("DriverLongitude", typeof(decimal));
        // The report says Ho Chi Minh City; the vehicle is in Ha Noi, about 20 km from the pickup point.
        ctx.Rows.Add(10L, 10.7769m, 106.7009m, 100L, 1L, 1L, 21.2000m, 105.7000m, 500_000m, "PayOS", reporterRole, 21.0285m, 105.8542m);
        sql.Results.Enqueue(ctx);

        var candidates = CandidateDriversTable();
        candidates.Rows.Add(2L, 20L, "Tài Cạnh Xe", "0900000002", "29A-00002", 1L, "Xe tải 500kg", 21.0290m, 105.8545m, Now.UtcDateTime, "Active", 200_000m);
        sql.Results.Enqueue(candidates);

        var result = await SenderWith(sql).Send(new GetReplacementDriversQuery(10));

        Assert.Equal(2L, Assert.Single(result).DriverProfileId);
    }

    [Fact]
    public async Task Drivers_with_gps_exactly_5_minutes_old_are_left_out_6_4()
    {
        var sql = new FakeSql();
        var ctx = IncidentContextTable();
        ctx.Rows.Add(10L, 21.0285m, 105.8542m, 100L, 1L, 1L, 21.0285m, 105.8542m, 500_000m, "PayOS");
        sql.Results.Enqueue(ctx);
        sql.Results.Enqueue(CandidateDriversTable());

        await SenderWith(sql).Send(new GetReplacementDriversQuery(10));

        Assert.Equal(Now.AddMinutes(-5).UtcDateTime, sql.LastParameters["FreshSince"]);
        Assert.Contains("dp.last_location_at > @FreshSince", sql.LastSql);
    }

    [Fact]
    public async Task NotFound_WhenIncidentDoesNotExist()
    {
        var sql = new FakeSql();
        sql.Results.Enqueue(IncidentContextTable());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SenderWith(sql).Send(new GetReplacementDriversQuery(999)));
    }
}

