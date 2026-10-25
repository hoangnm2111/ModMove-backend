using FluentValidation;

namespace ModMove.Application.Features.Incidents.Queries.ListIncidents;

internal sealed class ListIncidentsQueryValidator : AbstractValidator<ListIncidentsQuery>
{
    public ListIncidentsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.IncidentType).IsInEnum();
        RuleFor(x => x.Search).MaximumLength(20); // an order code (shipping_orders.order_code) is at most 20 characters
    }
}
