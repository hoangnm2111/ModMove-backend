using FluentValidation;

namespace ModMove.Application.Features.Incidents.Queries.GetMyIncidents;

internal sealed class GetMyIncidentsQueryValidator : AbstractValidator<GetMyIncidentsQuery>
{
    public GetMyIncidentsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
