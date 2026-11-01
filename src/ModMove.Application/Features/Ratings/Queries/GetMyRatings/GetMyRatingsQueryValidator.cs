using FluentValidation;

namespace ModMove.Application.Features.Ratings.Queries.GetMyRatings;

internal sealed class GetMyRatingsQueryValidator : AbstractValidator<GetMyRatingsQuery>
{
    public GetMyRatingsQueryValidator()
    {
        // Same bounds as the order list: (Page - 1) × PageSize stays inside int.
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
