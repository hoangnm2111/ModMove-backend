using FluentValidation;

namespace ModMove.Application.Features.Trips.Commands.CollectCash;

internal sealed class CollectCashCommandValidator : AbstractValidator<CollectCashCommand>
{
    public CollectCashCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        // GB-52: coordinates are optional and only range-checked; the server does not check the distance.
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).When(x => x.Latitude is not null);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).When(x => x.Longitude is not null);
    }
}

