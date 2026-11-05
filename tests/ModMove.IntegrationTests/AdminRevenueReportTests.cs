using System.Data;
using System.IO.Compression;
using System.Xml.Linq;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Reports.Queries.ExportRevenueReport;
using ModMove.Application.Features.Reports.Queries.GetRevenueReport;

namespace ModMove.IntegrationTests;

/// <summary>
/// Admin Revenue Report &amp; Excel Export (UC-ADM-21, UC-ADM-22, UC-ADM-23, DECISIONS B16, 8.1, 9.5):
/// Platform revenue = sum of commission_amount of Completed orders, grouped by Vietnam calendar day (UTC+7).
/// </summary>
public class AdminRevenueReportTests
{
    private static DataTable CreateOrdersTable()
    {
        var table = new DataTable();
        table.Columns.Add("OrderId", typeof(long));
        table.Columns.Add("OrderCode", typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("CompletedAt", typeof(DateTime));
        table.Columns.Add("CustomerName", typeof(string));
        table.Columns.Add("CustomerPhone", typeof(string));
        table.Columns.Add("DriverName", typeof(string));
        table.Columns.Add("LicensePlate", typeof(string));
        table.Columns.Add("VehicleTypeName", typeof(string));
        table.Columns.Add("EstimatedDistanceKm", typeof(decimal));
        table.Columns.Add("DistanceFee", typeof(decimal));
        table.Columns.Add("ServiceFee", typeof(decimal));
        table.Columns.Add("DiscountAmount", typeof(decimal));
        table.Columns.Add("TotalFee", typeof(decimal));
        table.Columns.Add("CommissionRate", typeof(decimal));
        table.Columns.Add("CommissionAmount", typeof(decimal));
        table.Columns.Add("PaymentMethod", typeof(string));
        table.Columns.Add("PaymentStatus", typeof(string));
        return table;
    }

    [Fact]
    public async Task Revenue_report_calculates_platform_revenue_from_commission_and_groups_by_Vietnam_date_UC_ADM_21_8_1_9_5()
    {
        var app = new TestApp();
        var table = CreateOrdersTable();

        // Order 2: Completed at 2026-10-20 18:30:00 UTC -> 2026-10-21 01:30:00 Vietnam time (day 2026-10-21)
        table.Rows.Add(
            2L,
            "MM20261020002",
            new DateTime(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc),
            "Trần Thị B",
            "0900000002",
            "Tài Xế 2",
            "29A-222.22",
            "Xe tải 1 tấn",
            15.5m,
            250_000m,
            50_000m,
            20_000m,
            280_000m,
            0.10m,
            28_000m,
            "PayOS",
            "Paid");

        // Order 1: Completed at 2026-10-20 16:59:59 UTC -> 2026-10-20 23:59:59 Vietnam time (day 2026-10-20)
        table.Rows.Add(
            1L,
            "MM20261020001",
            new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 20, 16, 59, 59, DateTimeKind.Utc),
            "Nguyễn Văn A",
            "0900000001",
            "Tài Xế 1",
            "29A-111.11",
            "Xe bán tải",
            10.0m,
            200_000m,
            0m,
            20_000m,
            180_000m,
            0.10m,
            18_000m,
            "Cash",
            "Paid");

        app.Sql.Result = table;

        var fromDate = new DateOnly(2026, 10, 20);
        var toDate = new DateOnly(2026, 10, 21);
        var report = await app.Sender.Send(new GetRevenueReportQuery(fromDate, toDate));

        // Check SQL Vietnam time boundary parameters: [2026-10-19 17:00:00Z, 2026-10-21 17:00:00Z)
        Assert.Equal(new DateTime(2026, 10, 19, 17, 0, 0, DateTimeKind.Utc), app.Sql.LastParameters["FromUtc"]);
        Assert.Equal(new DateTime(2026, 10, 21, 17, 0, 0, DateTimeKind.Utc), app.Sql.LastParameters["ToUtc"]);

        // DECISIONS 8.1: Platform revenue = sum of commission_amount (28,000 + 18,000 = 46,000), NOT total_fee (460,000)
        Assert.Equal(2, report.TotalCompletedOrders);
        Assert.Equal(46_000m, report.TotalRevenue);
        Assert.Equal(450_000m, report.TotalDeliveryFees); // Sum of DistanceFee (200,000 + 250,000)
        Assert.Equal(50_000m, report.TotalLoadingFees);
        Assert.Equal(40_000m, report.TotalDiscountAmount);
        Assert.Equal(414_000m, report.TotalDriverEarnings);

        // Grouped by Vietnam calendar day (UTC+7), ordered chronologically
        Assert.Equal(2, report.DailyStats.Count);
        Assert.Equal(new DateOnly(2026, 10, 20), report.DailyStats[0].Date);
        Assert.Equal((1, 18_000m, 200_000m, 162_000m),
            (report.DailyStats[0].CompletedOrders, report.DailyStats[0].Revenue, report.DailyStats[0].TotalDeliveryFees, report.DailyStats[0].DriverEarnings));

        Assert.Equal(new DateOnly(2026, 10, 21), report.DailyStats[1].Date);
        Assert.Equal((1, 28_000m, 250_000m, 252_000m),
            (report.DailyStats[1].CompletedOrders, report.DailyStats[1].Revenue, report.DailyStats[1].TotalDeliveryFees, report.DailyStats[1].DriverEarnings));
    }

    [Fact]
    public async Task Revenue_report_and_export_reject_invalid_date_ranges_UC_ADM_22_UC_ADM_23()
    {
        var app = new TestApp();

        // ToDate before FromDate
        await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(new GetRevenueReportQuery(new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 20))));

        // Range > 366 days
        await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(new ExportRevenueReportQuery(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 3))));
    }

    [Fact]
    public async Task Export_revenue_report_generates_valid_OpenXml_xlsx_archive_with_headers_and_Vietnam_timestamps_UC_ADM_23_B16()
    {
        var app = new TestApp();
        var table = CreateOrdersTable();
        table.Rows.Add(
            2L,
            "MM20261020002",
            new DateTime(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc),
            "Trần Thị B",
            "0900000002",
            "Tài Xế 2",
            "29A-222.22",
            "Xe tải 1 tấn",
            15.5m,
            250_000m,
            50_000m,
            20_000m,
            280_000m,
            0.10m,
            28_000m,
            "PayOS",
            "Paid");
        app.Sql.Result = table;

        var file = await app.Sender.Send(new ExportRevenueReportQuery(
            new DateOnly(2026, 10, 20),
            new DateOnly(2026, 10, 21)));

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.Equal("BaoCaoDoanhThu_20261020_20261021.xlsx", file.FileName);
        Assert.NotEmpty(file.Content);

        using var stream = new MemoryStream(file.Content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("_rels/.rels"));
        Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
        Assert.NotNull(archive.GetEntry("xl/_rels/workbook.xml.rels"));
        Assert.NotNull(archive.GetEntry("xl/styles.xml"));

        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheetEntry);

        using var sheetStream = sheetEntry.Open();
        var doc = XDocument.Load(sheetStream);
        var xmlText = doc.ToString();

        // Headers and row data in Vietnam time (18:30 UTC -> 2026-10-21 01:30:00 VN)
        Assert.Contains("Mã đơn", xmlText);
        Assert.Contains("Doanh thu hoa hồng (VND)", xmlText);
        Assert.Contains("Thu nhập tài xế (VND)", xmlText);
        Assert.Contains("MM20261020002", xmlText);
        Assert.Contains("2026-10-21 01:30:00", xmlText);
        Assert.Contains("28000", xmlText);
        Assert.Contains("252000", xmlText);
    }
}

