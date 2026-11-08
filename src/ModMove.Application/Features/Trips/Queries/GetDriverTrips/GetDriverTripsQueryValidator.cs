using FluentValidation;

namespace ModMove.Application.Features.Trips.Queries.GetDriverTrips;

internal sealed class GetDriverTripsQueryValidator : AbstractValidator<GetDriverTripsQuery>
{
    private static readonly DateOnly MinDate = new(2020, 1, 1);
    private static readonly DateOnly MaxDate = new(2100, 12, 31);

    public GetDriverTripsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.View).IsInEnum();
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.FromDate).InclusiveBetween(MinDate, MaxDate);
        RuleFor(x => x.ToDate).InclusiveBetween(MinDate, MaxDate);
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate)
            .When(x => x.FromDate is not null && x.ToDate is not null)
            .WithMessage("'To Date' must not be before 'From Date'.");
    }
}

