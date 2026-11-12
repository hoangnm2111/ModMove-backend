using FluentValidation;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Application.Features.DriverOnboarding.Commands.SubmitDriverApplication;

internal sealed class SubmitDriverApplicationCommandValidator : AbstractValidator<SubmitDriverApplicationCommand>
{
    /// <summary>Licence front, licence back, registration, inspection, vehicle photo, citizen ID front, citizen ID back (UC-DRV-03 step 3).</summary>
    internal const int RequiredDocumentCount = 7;

    public SubmitDriverApplicationCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x.VehicleTypeId).GreaterThan(0);

        RuleFor(x => x.CitizenIdNumber).NotEmpty().Matches("^[0-9]{9,12}$")
            .WithMessage("Citizen ID must be 9 to 12 digits.");

        RuleFor(x => x.DriverLicenseNumber).NotEmpty().Matches("^[0-9A-Za-z]{8,20}$")
            .WithMessage("Driver licence number must be 8 to 20 letters or digits.");

        RuleFor(x => x.LicensePlate).NotEmpty().MaximumLength(15).Matches(@"^[0-9A-Za-z.\- ]{5,15}$")
            .WithMessage("License plate has an invalid format.");

        RuleFor(x => x.MaxLoadWeightKg).GreaterThan(0m).LessThanOrEqualTo(100_000m); // column is DECIMAL(8,2)

        RuleFor(x => x.DocumentUrls).NotNull()
            .Must(d => d is { Count: RequiredDocumentCount })
            .WithMessage($"Exactly {RequiredDocumentCount} documents are required: licence front, licence back, registration, inspection, vehicle photo, citizen ID front, citizen ID back.");

        // security.md: images come only from the team's Cloudinary (uploaded through api/driver/uploads), never a link the
        // Administrator would open on another host.
        RuleForEach(x => x.DocumentUrls)
            .Must(url => url is { Length: > 0 and <= 500 } && storage.IsOwnedUrl(url))
            .WithMessage("Each document must be a photo uploaded to ModMove, at most 500 characters.")
            .When(x => x.DocumentUrls is not null);
    }
}

