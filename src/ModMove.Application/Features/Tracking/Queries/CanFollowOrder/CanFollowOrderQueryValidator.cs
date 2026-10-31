using FluentValidation;

namespace ModMove.Application.Features.Tracking.Queries.CanFollowOrder;

internal sealed class CanFollowOrderQueryValidator : AbstractValidator<CanFollowOrderQuery>
{
    public CanFollowOrderQueryValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.OrderId).GreaterThan(0);
    }
}
