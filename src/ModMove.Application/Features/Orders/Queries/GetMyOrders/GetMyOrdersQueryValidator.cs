using FluentValidation;

namespace ModMove.Application.Features.Orders.Queries.GetMyOrders;

internal sealed class GetMyOrdersQueryValidator : AbstractValidator<GetMyOrdersQuery>
{
    public GetMyOrdersQueryValidator()
    {
        // Upper bound keeps (Page - 1) × PageSize inside int; 10,000 pages of 50 is far beyond any customer's history.
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.View).IsInEnum();
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
