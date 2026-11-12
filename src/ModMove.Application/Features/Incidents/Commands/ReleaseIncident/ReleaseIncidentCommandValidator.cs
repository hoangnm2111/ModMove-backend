using FluentValidation;

namespace ModMove.Application.Features.Incidents.Commands.ReleaseIncident;

internal sealed class ReleaseIncidentCommandValidator : AbstractValidator<ReleaseIncidentCommand>
{
    public ReleaseIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).GreaterThan(0);
    }
}
