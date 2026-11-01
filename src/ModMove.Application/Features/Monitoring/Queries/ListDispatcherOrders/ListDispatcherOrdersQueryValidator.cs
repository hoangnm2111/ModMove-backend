using FluentValidation;

namespace ModMove.Application.Features.Monitoring.Queries.ListDispatcherOrders;

internal sealed class ListDispatcherOrdersQueryValidator : AbstractValidator<ListDispatcherOrdersQuery>
{
    public ListDispatcherOrdersQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("'To' must not be earlier than 'From'.");
    }
}

