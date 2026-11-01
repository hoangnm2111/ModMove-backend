using FluentValidation;

namespace ModMove.Application.Features.Incidents.Queries.GetReplacementDrivers;

internal sealed class GetReplacementDriversQueryValidator : AbstractValidator<GetReplacementDriversQuery>
{
    public GetReplacementDriversQueryValidator()
    {
        RuleFor(x => x.IncidentId).GreaterThan(0);
    }
}

