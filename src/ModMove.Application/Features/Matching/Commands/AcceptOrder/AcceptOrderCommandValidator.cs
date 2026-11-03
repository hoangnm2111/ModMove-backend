using FluentValidation;

namespace ModMove.Application.Features.Matching.Commands.AcceptOrder;

internal sealed class AcceptOrderCommandValidator : AbstractValidator<AcceptOrderCommand>
{
    public AcceptOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
    }
}

