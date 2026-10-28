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
    }
}
