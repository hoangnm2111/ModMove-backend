using FluentValidation;

namespace ModMove.Application.Features.Incidents.Commands.ResolveIncidentContinue;

internal sealed class ResolveIncidentContinueCommandValidator : AbstractValidator<ResolveIncidentContinueCommand>
{
    public ResolveIncidentContinueCommandValidator()
    {
        RuleFor(x => x.IncidentId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

