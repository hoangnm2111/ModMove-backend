using System.Globalization;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Reports.Queries.ExportRevenueReport;

internal sealed class ExportRevenueReportQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IExcelExporter excelExporter)
    : IQueryHandler<ExportRevenueReportQuery, RevenueReportFileResponse>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly IReadOnlyList<ExcelColumn<CompletedOrderRevenueDto>> Columns =
    [
        new("Mã đơn", o => o.OrderCode),
        new("Ngày tạo (VN)", o => RevenueReportData.ToVietnamTime(o.CreatedAt).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
        new("Ngày hoàn tất (VN)", o => RevenueReportData.ToVietnamTime(o.CompletedAt).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
        new("Khách hàng", o => o.CustomerName),
        new("SĐT khách", o => o.CustomerPhone),
        new("Tài xế", o => o.DriverName ?? ""),
        new("Biển số xe", o => o.LicensePlate ?? ""),
        new("Loại xe", o => o.VehicleTypeName),
        new("Quãng đường (km)", o => o.EstimatedDistanceKm),
        new("Cước vận chuyển (VND)", o => o.DistanceFee),
        new("Phí bốc xếp (VND)", o => o.ServiceFee),
        new("Giảm giá (VND)", o => o.DiscountAmount),
        new("Tổng cước (VND)", o => o.TotalFee),
        new("Tỷ lệ hoa hồng (%)", o => o.CommissionRate * 100m),
        new("Doanh thu hoa hồng (VND)", o => o.CommissionAmount),
        new("Thu nhập tài xế (VND)", o => o.DriverEarning),
        new("Phương thức thanh toán", o => o.PaymentMethod),
        new("Trạng thái thanh toán", o => o.PaymentStatus),
    ];

    public async Task<RevenueReportFileResponse> Handle(ExportRevenueReportQuery request, CancellationToken cancellationToken)
    {
        var report = await RevenueReportData.LoadAsync(connectionFactory, request.FromDate, request.ToDate, cancellationToken);
        var content = excelExporter.ExportToExcel("DoanhThu", Columns, report.Orders);
        var fileName = BuildFileName(request.FromDate, request.ToDate);

        return new RevenueReportFileResponse(content, ExcelContentType, fileName);
    }

    private static string BuildFileName(DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate is not null && toDate is not null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"BaoCaoDoanhThu_{fromDate.Value:yyyyMMdd}_{toDate.Value:yyyyMMdd}.xlsx");
        }

        if (fromDate is not null)
            return string.Create(CultureInfo.InvariantCulture, $"BaoCaoDoanhThu_Tu_{fromDate.Value:yyyyMMdd}.xlsx");

        if (toDate is not null)
            return string.Create(CultureInfo.InvariantCulture, $"BaoCaoDoanhThu_Den_{toDate.Value:yyyyMMdd}.xlsx");

        return "BaoCaoDoanhThu_TatCa.xlsx";
    }
}

