using FluentValidation;

namespace ModMove.Application.Features.Reports.Queries.ExportApprovedPayouts;

internal sealed class ExportApprovedPayoutsQueryValidator : AbstractValidator<ExportApprovedPayoutsQuery>
{
    public ExportApprovedPayoutsQueryValidator()
    {
        When(x => x.FromDate.HasValue && x.ToDate.HasValue, () =>
        {
            RuleFor(x => x.ToDate!.Value)
                .GreaterThanOrEqualTo(x => x.FromDate!.Value)
                .WithMessage("ToDate must be greater than or equal to FromDate.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Role), () =>
        {
            RuleFor(x => x.Role)
                .Must(r => r is null || r.Equals("All", StringComparison.OrdinalIgnoreCase)
                                     || r.Equals("Driver", StringComparison.OrdinalIgnoreCase)
                                     || r.Equals("Customer", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Role filter must be 'All', 'Driver', or 'Customer'.");
        });
    }
}
