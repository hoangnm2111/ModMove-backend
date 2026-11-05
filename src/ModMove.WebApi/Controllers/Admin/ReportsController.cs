using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Reports.Queries.ExportRevenueReport;
using ModMove.Application.Features.Reports.Queries.GetRevenueReport;

namespace ModMove.WebApi.Controllers.Admin;

/// <summary>
/// Admin only (RoleConvention): api/admin/reports.
/// Screen #38: Revenue report (total commission of Completed orders in Vietnam time) and Excel (.xlsx) export
/// (UC-ADM-21, UC-ADM-22, UC-ADM-23, DECISIONS B16, 8.1, 9.5).
/// </summary>
public sealed class ReportsController : ApiControllerBase
{
    [HttpGet("revenue")]
    public Task<RevenueReportResponse> GetRevenue(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken cancellationToken = default) =>
        Sender.Send(new GetRevenueReportQuery(fromDate, toDate), cancellationToken);

    [HttpGet("revenue/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportRevenue(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var file = await Sender.Send(new ExportRevenueReportQuery(fromDate, toDate), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}

