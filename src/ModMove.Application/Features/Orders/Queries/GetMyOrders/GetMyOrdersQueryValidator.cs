using FluentValidation;

namespace ModMove.Application.Features.Orders.Queries.GetMyOrders;

internal sealed class GetMyOrdersQueryValidator : AbstractValidator<GetMyOrdersQuery>
{
    public GetMyOrdersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.View).IsInEnum();
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
