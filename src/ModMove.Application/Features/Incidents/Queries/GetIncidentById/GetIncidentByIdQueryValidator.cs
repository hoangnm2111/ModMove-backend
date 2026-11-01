using FluentValidation;

namespace ModMove.Application.Features.Incidents.Queries.GetIncidentById;

internal sealed class GetIncidentByIdQueryValidator : AbstractValidator<GetIncidentByIdQuery>
{
    public GetIncidentByIdQueryValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
