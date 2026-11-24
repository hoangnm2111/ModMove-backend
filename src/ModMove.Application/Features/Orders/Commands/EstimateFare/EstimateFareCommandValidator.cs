using FluentValidation;

namespace ModMove.Application.Features.Orders.Commands.EstimateFare;

internal sealed class EstimateFareCommandValidator : AbstractValidator<EstimateFareCommand>
{
    public EstimateFareCommandValidator()
    {
        RuleFor(x => x.VehicleTypeId).GreaterThan(0);
        RuleFor(x => x.PickupAddress).NotEmpty().MaximumLength(255);
        RuleFor(x => x.DropoffAddress).NotEmpty().MaximumLength(255);
        RuleFor(x => x.WeightKg).GreaterThan(0).LessThanOrEqualTo(100_000);
        RuleFor(x => x.LengthCm).InclusiveBetween(1, 2_000);
        RuleFor(x => x.WidthCm).InclusiveBetween(1, 2_000);
        RuleFor(x => x.HeightCm).InclusiveBetween(1, 2_000);
        RuleFor(x => x.CouponCode).MaximumLength(50);
        RuleFor(x => x.DropoffAddress)
            .Must((x, dropoff) => !string.Equals(x.PickupAddress?.Trim(), dropoff?.Trim(), StringComparison.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrWhiteSpace(x.PickupAddress) && !string.IsNullOrWhiteSpace(x.DropoffAddress))
            .WithMessage("The drop-off address must differ from the pickup address.");
    }
}
