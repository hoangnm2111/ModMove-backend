using FluentValidation;

namespace ModMove.Application.Features.Trips.Commands.CollectCash;

internal sealed class CollectCashCommandValidator : AbstractValidator<CollectCashCommand>
{
    public CollectCashCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.Latitude)
            .NotNull().WithMessage("Current latitude is required.")
            .InclusiveBetween(-90m, 90m);
        RuleFor(x => x.Longitude)
            .NotNull().WithMessage("Current longitude is required.")
            .InclusiveBetween(-180m, 180m);
    }
}

