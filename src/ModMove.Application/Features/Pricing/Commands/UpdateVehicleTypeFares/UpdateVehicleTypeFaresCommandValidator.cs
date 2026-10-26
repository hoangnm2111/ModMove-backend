using FluentValidation;

namespace ModMove.Application.Features.Pricing.Commands.UpdateVehicleTypeFares;

internal sealed class UpdateVehicleTypeFaresCommandValidator : AbstractValidator<UpdateVehicleTypeFaresCommand>
{
    // fares columns are DECIMAL(15,2); the cap keeps an absurd number a 400 instead of a database error.
    internal const decimal MaxAmount = 1_000_000_000m;

    public UpdateVehicleTypeFaresCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.BaseFare).InclusiveBetween(0m, MaxAmount);
        RuleFor(x => x.PricePerKm).GreaterThan(0m).LessThanOrEqualTo(MaxAmount);
        RuleFor(x => x.MinimumFare).InclusiveBetween(0m, MaxAmount);
    }
}
