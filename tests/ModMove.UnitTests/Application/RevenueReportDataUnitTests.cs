using ModMove.Application.Features.Reports;

namespace ModMove.UnitTests.Application;

/// <summary>
/// Unit tests for Vietnam timezone calculations in RevenueReportData (UTC+7, DECISIONS 8.1, 9.5).
/// </summary>
public class RevenueReportDataUnitTests
{
    [Fact]
    public void ToVietnamDate_maps_utc_before_17h_to_same_calendar_day_happy_case()
    {
        // 2026-10-20 10:00:00 UTC -> 17:00:00 VN -> Date: 2026-10-20
        var utc = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
        var date = RevenueReportData.ToVietnamDate(utc);

        Assert.Equal(new DateOnly(2026, 10, 20), date);
    }

    [Fact]
    public void ToVietnamDate_maps_16h59m59s_utc_to_current_day_edge_case()
    {
        // 2026-10-20 16:59:59 UTC -> 2026-10-20 23:59:59 VN -> Date: 2026-10-20
        var utc = new DateTimeOffset(2026, 10, 20, 16, 59, 59, TimeSpan.Zero);
        var date = RevenueReportData.ToVietnamDate(utc);

        Assert.Equal(new DateOnly(2026, 10, 20), date);
    }

    [Fact]
    public void ToVietnamDate_maps_17h00m00s_utc_to_next_calendar_day_edge_case()
    {
        // 2026-10-20 17:00:00 UTC -> 2026-10-21 00:00:00 VN -> Date: 2026-10-21
        var utc = new DateTimeOffset(2026, 10, 20, 17, 0, 0, TimeSpan.Zero);
        var date = RevenueReportData.ToVietnamDate(utc);

        Assert.Equal(new DateOnly(2026, 10, 21), date);
    }

    [Fact]
    public void ToVietnamDate_crosses_month_and_year_boundary_properly_edge_case()
    {
        // 2026-12-31 17:00:00 UTC -> 2027-01-01 00:00:00 VN
        var utc = new DateTimeOffset(2026, 12, 31, 17, 0, 0, TimeSpan.Zero);
        var date = RevenueReportData.ToVietnamDate(utc);

        Assert.Equal(new DateOnly(2027, 1, 1), date);
    }

    [Fact]
    public void ToVietnamDate_handles_leap_year_transition_properly_edge_case()
    {
        // 2024-02-29 17:00:00 UTC -> 2024-03-01 00:00:00 VN
        var utc = new DateTimeOffset(2024, 2, 29, 17, 0, 0, TimeSpan.Zero);
        var date = RevenueReportData.ToVietnamDate(utc);

        Assert.Equal(new DateOnly(2024, 3, 1), date);
    }

    [Fact]
    public void ToVietnamTime_converts_utc_to_plus_7_offset()
    {
        var utc = new DateTimeOffset(2026, 10, 20, 15, 30, 0, TimeSpan.Zero);
        var vn = RevenueReportData.ToVietnamTime(utc);

        Assert.Equal(TimeSpan.FromHours(7), vn.Offset);
        Assert.Equal(new DateTime(2026, 10, 20, 22, 30, 0), vn.DateTime);
    }
}

