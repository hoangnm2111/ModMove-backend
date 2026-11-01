using FluentValidation;

namespace ModMove.Application.Features.Incidents.Commands.StartIncidentHandling;

internal sealed class StartIncidentHandlingCommandValidator : AbstractValidator<StartIncidentHandlingCommand>
{
    public StartIncidentHandlingCommandValidator()
    {
        RuleFor(x => x.IncidentId).GreaterThan(0);
    }
}

