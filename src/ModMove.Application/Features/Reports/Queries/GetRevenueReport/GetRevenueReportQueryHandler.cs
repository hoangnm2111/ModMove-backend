using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Reports.Queries.GetRevenueReport;

internal sealed class GetRevenueReportQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetRevenueReportQuery, RevenueReportResponse>
{
    public Task<RevenueReportResponse> Handle(GetRevenueReportQuery request, CancellationToken cancellationToken) =>
        RevenueReportData.LoadAsync(connectionFactory, request.FromDate, request.ToDate, cancellationToken);
}

