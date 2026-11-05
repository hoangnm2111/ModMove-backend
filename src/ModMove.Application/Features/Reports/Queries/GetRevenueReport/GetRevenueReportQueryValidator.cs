using FluentValidation;

namespace ModMove.Application.Features.Reports.Queries.GetRevenueReport;

internal sealed class GetRevenueReportQueryValidator : AbstractValidator<GetRevenueReportQuery>
{
    public GetRevenueReportQueryValidator() =>
        RevenueReportData.AddDateRangeRules(this, x => x.FromDate, x => x.ToDate);
}

