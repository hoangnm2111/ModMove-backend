using FluentValidation.TestHelper;
using ModMove.Application.Features.Reports.Queries.ExportRevenueReport;
using ModMove.Application.Features.Reports.Queries.GetRevenueReport;

namespace ModMove.UnitTests.Application;

/// <summary>
/// Unit tests for Revenue Report Validators (GetRevenueReportQueryValidator and ExportRevenueReportQueryValidator).
/// Covers happy paths, boundary conditions, and edge cases (UC-ADM-22, UC-ADM-23).
/// </summary>
public class RevenueReportValidatorTests
{
    private readonly GetRevenueReportQueryValidator _getValidator = new();
    private readonly ExportRevenueReportQueryValidator _exportValidator = new();

    // ==========================================
    // 1. Happy Cases
    // ==========================================

    [Fact]
    public void Validator_passes_when_both_dates_are_null_happy_case()
    {
        var getQuery = new GetRevenueReportQuery(null, null);
        var exportQuery = new ExportRevenueReportQuery(null, null);

        _getValidator.TestValidate(getQuery).ShouldNotHaveAnyValidationErrors();
        _exportValidator.TestValidate(exportQuery).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_passes_when_date_range_is_valid_happy_case()
    {
        var fromDate = new DateOnly(2026, 10, 1);
        var toDate = new DateOnly(2026, 10, 31);

        var getQuery = new GetRevenueReportQuery(fromDate, toDate);
        var exportQuery = new ExportRevenueReportQuery(fromDate, toDate);

        _getValidator.TestValidate(getQuery).ShouldNotHaveAnyValidationErrors();
        _exportValidator.TestValidate(exportQuery).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_passes_when_from_and_to_dates_are_the_same_day_happy_case()
    {
        var sameDay = new DateOnly(2026, 10, 20);

        var getQuery = new GetRevenueReportQuery(sameDay, sameDay);
        var exportQuery = new ExportRevenueReportQuery(sameDay, sameDay);

        _getValidator.TestValidate(getQuery).ShouldNotHaveAnyValidationErrors();
        _exportValidator.TestValidate(exportQuery).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_passes_when_only_FromDate_or_only_ToDate_is_provided_happy_case()
    {
        var validDate = new DateOnly(2026, 10, 20);

        _getValidator.TestValidate(new GetRevenueReportQuery(validDate, null)).ShouldNotHaveAnyValidationErrors();
        _getValidator.TestValidate(new GetRevenueReportQuery(null, validDate)).ShouldNotHaveAnyValidationErrors();

        _exportValidator.TestValidate(new ExportRevenueReportQuery(validDate, null)).ShouldNotHaveAnyValidationErrors();
        _exportValidator.TestValidate(new ExportRevenueReportQuery(null, validDate)).ShouldNotHaveAnyValidationErrors();
    }

    // ==========================================
    // 2. Boundary / Edge Cases
    // ==========================================

    [Fact]
    public void Validator_passes_at_exact_366_days_limit_edge_case()
    {
        // 2025-01-01 to 2026-01-02 is exactly 366 days
        var fromDate = new DateOnly(2025, 1, 1);
        var toDate = new DateOnly(2026, 1, 2);
        Assert.Equal(366, toDate.DayNumber - fromDate.DayNumber);

        var getQuery = new GetRevenueReportQuery(fromDate, toDate);
        var exportQuery = new ExportRevenueReportQuery(fromDate, toDate);

        _getValidator.TestValidate(getQuery).ShouldNotHaveAnyValidationErrors();
        _exportValidator.TestValidate(exportQuery).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_fails_when_range_exceeds_366_days_edge_case()
    {
        // 2025-01-01 to 2026-01-03 is 367 days
        var fromDate = new DateOnly(2025, 1, 1);
        var toDate = new DateOnly(2026, 1, 3);
        Assert.Equal(367, toDate.DayNumber - fromDate.DayNumber);

        var getResult = _getValidator.TestValidate(new GetRevenueReportQuery(fromDate, toDate));
        getResult.ShouldHaveValidationErrorFor(x => x.ToDate)
            .WithErrorMessage("The report date range must not exceed 366 days.");

        var exportResult = _exportValidator.TestValidate(new ExportRevenueReportQuery(fromDate, toDate));
        exportResult.ShouldHaveValidationErrorFor(x => x.ToDate)
            .WithErrorMessage("The report date range must not exceed 366 days.");
    }

    [Fact]
    public void Validator_fails_when_ToDate_is_before_FromDate_edge_case()
    {
        var fromDate = new DateOnly(2026, 10, 20);
        var toDate = new DateOnly(2026, 10, 19);

        var getResult = _getValidator.TestValidate(new GetRevenueReportQuery(fromDate, toDate));
        getResult.ShouldHaveValidationErrorFor(x => x.ToDate)
            .WithErrorMessage("'To Date' must not be before 'From Date'.");

        var exportResult = _exportValidator.TestValidate(new ExportRevenueReportQuery(fromDate, toDate));
        exportResult.ShouldHaveValidationErrorFor(x => x.ToDate)
            .WithErrorMessage("'To Date' must not be before 'From Date'.");
    }

    [Theory]
    [InlineData(2019, 12, 31)] // before 2020-01-01
    [InlineData(1990, 5, 10)]
    public void Validator_fails_when_FromDate_is_before_min_bound_edge_case(int year, int month, int day)
    {
        var outOfRange = new DateOnly(year, month, day);

        var getResult = _getValidator.TestValidate(new GetRevenueReportQuery(outOfRange, null));
        getResult.ShouldHaveValidationErrorFor(x => x.FromDate);

        var exportResult = _exportValidator.TestValidate(new ExportRevenueReportQuery(outOfRange, null));
        exportResult.ShouldHaveValidationErrorFor(x => x.FromDate);
    }

    [Theory]
    [InlineData(2101, 1, 1)] // after 2100-12-31
    [InlineData(2150, 6, 1)]
    public void Validator_fails_when_ToDate_is_after_max_bound_edge_case(int year, int month, int day)
    {
        var outOfRange = new DateOnly(year, month, day);

        var getResult = _getValidator.TestValidate(new GetRevenueReportQuery(null, outOfRange));
        getResult.ShouldHaveValidationErrorFor(x => x.ToDate);

        var exportResult = _exportValidator.TestValidate(new ExportRevenueReportQuery(null, outOfRange));
        exportResult.ShouldHaveValidationErrorFor(x => x.ToDate);
    }
}

