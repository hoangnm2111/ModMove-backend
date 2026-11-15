using System.Globalization;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Reports.Queries.ExportApprovedPayouts;

internal sealed class ExportApprovedPayoutsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IExcelExporter excelExporter,
    TimeProvider clock)
    : IQueryHandler<ExportApprovedPayoutsQuery, ApprovedPayoutsFileResponse>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly IReadOnlyList<ExcelColumn<ApprovedPayoutItemDto>> Columns =
    [
        new("STT", x => x.Index),
        new("Mã tham chiếu", x => x.PayoutReference),
        new("Loại giao dịch", x => x.PayoutType),
        new("Đối tượng (Role)", x => x.Role),
        new("Tên người thụ hưởng", x => x.BeneficiaryName),
        new("Số tài khoản nhận", x => x.BeneficiaryAccountNumber),
        new("Ngân hàng nhận", x => x.BeneficiaryBankName),
        new("Mã BIN ngân hàng", x => x.BeneficiaryBankBin ?? ""),
        new("Số tiền (VND)", x => x.Amount),
        new("Nội dung chuyển khoản", x => x.TransferDescription),
        new("Số điện thoại", x => x.PhoneNumber),
        new("Mã đơn hàng liên quan", x => x.OrderCode ?? "-"),
        new("Ngày phê duyệt (VN)", x => x.ApprovedAt is not null
            ? ApprovedPayoutsDataLoader.ToVietnamTime(x.ApprovedAt.Value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : "-"),
        new("Người duyệt", x => x.ApprovedByName ?? "Admin"),
    ];

    public async Task<ApprovedPayoutsFileResponse> Handle(ExportApprovedPayoutsQuery request, CancellationToken cancellationToken)
    {
        var rows = await ApprovedPayoutsDataLoader.LoadAsync(
            connectionFactory, request.FromDate, request.ToDate, request.Role, cancellationToken);

        var content = excelExporter.ExportToExcel("LenhChiHo", Columns, rows);
        var fileName = BuildFileName(request.FromDate, request.ToDate, request.Role, clock.GetUtcNow());

        return new ApprovedPayoutsFileResponse(content, ExcelContentType, fileName);
    }

    private static string BuildFileName(DateOnly? fromDate, DateOnly? toDate, string? role, DateTimeOffset now)
    {
        var timestamp = ApprovedPayoutsDataLoader.ToVietnamTime(now).ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var roleSuffix = string.IsNullOrWhiteSpace(role) || role.Equals("All", StringComparison.OrdinalIgnoreCase)
            ? "TatCa"
            : role.Trim();

        if (fromDate is not null && toDate is not null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"BangKeChiHo_{roleSuffix}_{fromDate.Value:yyyyMMdd}_{toDate.Value:yyyyMMdd}.xlsx");
        }

        if (fromDate is not null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"BangKeChiHo_{roleSuffix}_Tu_{fromDate.Value:yyyyMMdd}.xlsx");
        }

        if (toDate is not null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"BangKeChiHo_{roleSuffix}_Den_{toDate.Value:yyyyMMdd}.xlsx");
        }

        return $"BangKeChiHo_{roleSuffix}_{timestamp}.xlsx";
    }
}
