using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Reports.Queries.ExportRevenueReport;

/// <summary>
/// Exports the Admin revenue report as a standard Office Open XML (.xlsx) file (UC-ADM-23, DECISIONS B16, 8.1, 9.5).
/// </summary>
public sealed record ExportRevenueReportQuery(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IQuery<RevenueReportFileResponse>;

public sealed record RevenueReportFileResponse(
    byte[] Content,
    string ContentType,
    string FileName);

