using FluentValidation;

namespace ModMove.Application.Features.Ratings.Commands.RateTrip;

internal sealed class RateTripCommandValidator : AbstractValidator<RateTripCommand>
{
    public RateTripCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.Stars).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000); // ratings.review_comment
    }
}
