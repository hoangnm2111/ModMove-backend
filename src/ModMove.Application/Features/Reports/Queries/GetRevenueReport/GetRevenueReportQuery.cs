using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Reports.Queries.GetRevenueReport;

/// <summary>
/// Admin revenue report (UC-ADM-21, UC-ADM-22, DECISIONS 8.1, 9.5):
/// Platform revenue equals the sum of <c>commission_amount</c> of Completed orders in Vietnam time (UTC+7).
/// </summary>
public sealed record GetRevenueReportQuery(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IQuery<RevenueReportResponse>;

public sealed record RevenueReportResponse(
    DateOnly? FromDate,
    DateOnly? ToDate,
    int TotalCompletedOrders,
    decimal TotalRevenue,
    decimal TotalDeliveryFees,
    decimal TotalLoadingFees,
    decimal TotalDiscountAmount,
    decimal TotalDriverEarnings,
    IReadOnlyList<DailyRevenueDto> DailyStats,
    IReadOnlyList<CompletedOrderRevenueDto> Orders);

