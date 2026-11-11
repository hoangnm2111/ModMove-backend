using FluentValidation;

namespace ModMove.Application.Features.Incidents.Queries.GetMyIncidents;

internal sealed class GetMyIncidentsQueryValidator : AbstractValidator<GetMyIncidentsQuery>
{
    public GetMyIncidentsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.OrderId).GreaterThan(0).When(x => x.OrderId is not null);
        RuleFor(x => x.Status).IsInEnum();
    }
}
