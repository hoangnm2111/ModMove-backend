using FluentValidation;

namespace ModMove.Application.Features.Reports.Queries.ExportRevenueReport;

internal sealed class ExportRevenueReportQueryValidator : AbstractValidator<ExportRevenueReportQuery>
{
    public ExportRevenueReportQueryValidator() =>
        RevenueReportData.AddDateRangeRules(this, x => x.FromDate, x => x.ToDate);
}

