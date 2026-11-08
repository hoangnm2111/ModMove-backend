using FluentValidation;

namespace ModMove.Application.Features.Incidents.Queries.GetDriverIncidents;

internal sealed class GetDriverIncidentsQueryValidator : AbstractValidator<GetDriverIncidentsQuery>
{
    public GetDriverIncidentsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.OrderId).GreaterThan(0).When(x => x.OrderId is not null);
        RuleFor(x => x.Status).IsInEnum();
    }
}

internal sealed class GetDriverIncidentDetailQueryValidator : AbstractValidator<GetDriverIncidentDetailQuery>
{
    public GetDriverIncidentDetailQueryValidator()
    {
        RuleFor(x => x.IncidentId).GreaterThan(0);
    }
}

