using FluentValidation;

namespace ModMove.Application.Features.Incidents.Commands.ReassignIncidentDriver;

internal sealed class ReassignIncidentDriverCommandValidator : AbstractValidator<ReassignIncidentDriverCommand>
{
    public ReassignIncidentDriverCommandValidator()
    {
        RuleFor(x => x.IncidentId).GreaterThan(0);
        RuleFor(x => x.NewDriverProfileId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

