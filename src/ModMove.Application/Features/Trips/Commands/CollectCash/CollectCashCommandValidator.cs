using FluentValidation;

namespace ModMove.Application.Features.Trips.Commands.CollectCash;

internal sealed class CollectCashCommandValidator : AbstractValidator<CollectCashCommand>
{
    public CollectCashCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
    }
}

