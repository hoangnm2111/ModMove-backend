using FluentValidation;

namespace ModMove.Application.Features.Ratings.Queries.GetDriverRatings;

internal sealed class GetDriverRatingsQueryValidator : AbstractValidator<GetDriverRatingsQuery>
{
    public GetDriverRatingsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Stars)
            .InclusiveBetween(1, 5)
            .When(x => x.Stars.HasValue);
    }
}

