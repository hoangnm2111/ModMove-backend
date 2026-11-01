using FluentValidation;

namespace ModMove.Application.Features.Incidents.Commands.CancelIncident;

internal sealed class CancelIncidentCommandValidator : AbstractValidator<CancelIncidentCommand>
{
    public CancelIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(950);
    }
}

