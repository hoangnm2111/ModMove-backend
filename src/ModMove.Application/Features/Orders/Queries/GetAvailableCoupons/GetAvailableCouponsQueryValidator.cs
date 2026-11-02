using FluentValidation;

namespace ModMove.Application.Features.Orders.Queries.GetAvailableCoupons;

internal sealed class GetAvailableCouponsQueryValidator : AbstractValidator<GetAvailableCouponsQuery>
{
    public GetAvailableCouponsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
