using FluentValidation;

namespace ModMove.Application.Features.Trips.Commands.ConfirmArrival;

internal sealed class ConfirmArrivalCommandValidator : AbstractValidator<ConfirmArrivalCommand>
{
    public ConfirmArrivalCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
    }
}

