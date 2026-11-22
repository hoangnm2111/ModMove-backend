using FluentValidation;
using ModMove.Application.Features.Pricing.Commands.UpdateVehicleTypeFares;

namespace ModMove.Application.Features.Pricing.Commands.CreateVehicleType;

internal sealed class CreateVehicleTypeCommandValidator : AbstractValidator<CreateVehicleTypeCommand>
{
    // Sizes are INT cm; the caps keep absurd values a 400 instead of a database error.
    internal const int MaxSizeCm = 2_000;
    internal const decimal MaxWeightKg = 50_000m;

    public CreateVehicleTypeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.MaxLoadWeightKg).GreaterThan(0m).LessThanOrEqualTo(MaxWeightKg);
        RuleFor(x => x.CargoBedLengthCm).InclusiveBetween(1, MaxSizeCm);
        RuleFor(x => x.CargoBedWidthCm).InclusiveBetween(1, MaxSizeCm);
        RuleFor(x => x.CargoBedHeightCm).InclusiveBetween(1, MaxSizeCm);
        RuleFor(x => x.BaseFare).InclusiveBetween(0m, UpdateVehicleTypeFaresCommandValidator.MaxAmount);
        RuleFor(x => x.PricePerKm).GreaterThan(0m).LessThanOrEqualTo(UpdateVehicleTypeFaresCommandValidator.MaxAmount);
        RuleFor(x => x.MinimumFare).InclusiveBetween(0m, UpdateVehicleTypeFaresCommandValidator.MaxAmount);
    }
}
