using FluentValidation;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverEarnings;

internal sealed class GetDriverEarningsQueryValidator : AbstractValidator<GetDriverEarningsQuery>
{
    private static readonly DateOnly MinDate = new(2020, 1, 1);
    private static readonly DateOnly MaxDate = new(2100, 12, 31);

    public GetDriverEarningsQueryValidator()
    {
        RuleFor(x => x.Period).IsInEnum();
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.FromDate).InclusiveBetween(MinDate, MaxDate);
        RuleFor(x => x.ToDate).InclusiveBetween(MinDate, MaxDate);
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate)
            .When(x => x.FromDate is not null && x.ToDate is not null)
            .WithMessage("'To Date' must not be before 'From Date'.");
    }
}

