using FluentValidation;

namespace ModMove.Application.Features.Incidents.Commands.CancelOrderByIncident;

internal sealed class CancelOrderByIncidentCommandValidator : AbstractValidator<CancelOrderByIncidentCommand>
{
    public CancelOrderByIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).GreaterThan(0);
        // shipping_orders.cancellation_reason is VARCHAR(500); leave room for the optional "[Cargo: ...]" suffix.
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(450);
        RuleFor(x => x.CargoDisposition).IsInEnum().When(x => x.CargoDisposition is not null);
    }
}

