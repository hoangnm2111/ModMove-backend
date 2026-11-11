using FluentValidation;
using ModMove.Application.Features.Pricing.Commands.UpdateVehicleTypeFares;

namespace ModMove.Application.Features.Pricing.Commands.UpdatePlatformSettings;

internal sealed class UpdatePlatformSettingsCommandValidator : AbstractValidator<UpdatePlatformSettingsCommand>
{
    public UpdatePlatformSettingsCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.CommissionRate is not null || x.LoadingFee is not null
                       || x.RequiredDeposit is not null || x.MatchingRadiusKm is not null)
            .WithMessage("Provide at least one setting to change.");

        RuleFor(x => x.CommissionRate!.Value).InclusiveBetween(0m, 1m)
            .WithName(nameof(UpdatePlatformSettingsCommand.CommissionRate))
            .When(x => x.CommissionRate is not null);

        RuleFor(x => x.LoadingFee!.Value).InclusiveBetween(0m, UpdateVehicleTypeFaresCommandValidator.MaxAmount)
            .WithName(nameof(UpdatePlatformSettingsCommand.LoadingFee))
            .When(x => x.LoadingFee is not null);

        RuleFor(x => x.RequiredDeposit!.Value).InclusiveBetween(0m, UpdateVehicleTypeFaresCommandValidator.MaxAmount)
            .WithName(nameof(UpdatePlatformSettingsCommand.RequiredDeposit))
            .When(x => x.RequiredDeposit is not null);

        RuleFor(x => x.MatchingRadiusKm!.Value).GreaterThan(0m).LessThanOrEqualTo(1_000m)
            .WithName(nameof(UpdatePlatformSettingsCommand.MatchingRadiusKm))
            .When(x => x.MatchingRadiusKm is not null);
    }
}
