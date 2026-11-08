using FluentValidation;

namespace ModMove.Application.Features.Matching.Queries.GetNearbyOrderDetail;

internal sealed class GetNearbyOrderDetailQueryValidator : AbstractValidator<GetNearbyOrderDetailQuery>
{
    public GetNearbyOrderDetailQueryValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
    }
}

