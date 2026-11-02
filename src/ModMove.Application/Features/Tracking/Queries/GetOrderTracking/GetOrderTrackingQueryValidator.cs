using FluentValidation;

namespace ModMove.Application.Features.Tracking.Queries.GetOrderTracking;

internal sealed class GetOrderTrackingQueryValidator : AbstractValidator<GetOrderTrackingQuery>
{
    public GetOrderTrackingQueryValidator() => RuleFor(x => x.OrderId).GreaterThan(0);
}
