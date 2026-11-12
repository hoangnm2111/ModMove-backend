using FluentValidation;

namespace ModMove.Application.Features.Incidents.Commands.ReportIncidentByDispatcher;

internal sealed class ReportIncidentByDispatcherCommandValidator : AbstractValidator<ReportIncidentByDispatcherCommand>
{
    public ReportIncidentByDispatcherCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.IncidentType).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000); // in_transit_incidents.description
    }
}
