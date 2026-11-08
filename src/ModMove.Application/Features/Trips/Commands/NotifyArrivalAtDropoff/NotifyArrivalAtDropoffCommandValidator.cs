using FluentValidation;

namespace ModMove.Application.Features.Trips.Commands.NotifyArrivalAtDropoff;

internal sealed class NotifyArrivalAtDropoffCommandValidator : AbstractValidator<NotifyArrivalAtDropoffCommand>
{
    public NotifyArrivalAtDropoffCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
    }
}

