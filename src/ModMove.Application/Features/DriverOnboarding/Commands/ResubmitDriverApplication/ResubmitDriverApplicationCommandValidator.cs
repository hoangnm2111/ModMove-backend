using System.Text.RegularExpressions;
using FluentValidation;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Constants;

namespace ModMove.Application.Features.DriverOnboarding.Commands.ResubmitDriverApplication;

internal sealed class ResubmitDriverApplicationCommandValidator : AbstractValidator<ResubmitDriverApplicationCommand>
{
    public ResubmitDriverApplicationCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x)
            .Must(HasAnyPayload)
            .WithMessage("Provide at least one updated document item, vehicle update, or personal info update.");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.Key)
                    .Must(k =>
                    {
                        var normalized = DriverApplicationItemKeys.Normalize(k);
                        return normalized is not null
                            && (DriverApplicationItemKeys.DocumentIndexByKey.ContainsKey(normalized)
                                || normalized == DriverApplicationItemKeys.Portrait);
                    })
                    .WithMessage("Invalid document item key.");

                item.RuleFor(i => i.Url)
                    .Must(url => url is { Length: > 0 and <= 500 } && storage.IsOwnedUrl(url.Trim()))
                    .WithMessage("Each document must be a photo uploaded to ModMove, at most 500 characters.");
            })
            .When(x => x.Items is not null);

        When(x => x.Vehicle is not null, () =>
        {
            RuleFor(x => x.Vehicle!.VehicleTypeId)
                .GreaterThan(0)
                .When(x => x.Vehicle!.VehicleTypeId is not null);

            RuleFor(x => x.Vehicle!.LicensePlate)
                .NotEmpty()
                .MaximumLength(15)
                .Matches(@"^[0-9A-Za-z.\- ]{5,15}$")
                .WithMessage("License plate has an invalid format.")
                .When(x => x.Vehicle!.LicensePlate is not null);

            RuleFor(x => x.Vehicle!.MaxLoadWeightKg)
                .GreaterThan(0m)
                .LessThanOrEqualTo(100_000m)
                .When(x => x.Vehicle!.MaxLoadWeightKg is not null);
        });

        When(x => x.PersonalInfo is not null, () =>
        {
            RuleFor(x => x.PersonalInfo!.CitizenIdNumber)
                .Must(v => Regex.IsMatch(v!.Trim(), "^[0-9]{9,12}$"))
                .WithMessage("Citizen ID must be 9 to 12 digits.")
                .When(x => !string.IsNullOrWhiteSpace(x.PersonalInfo!.CitizenIdNumber));

            RuleFor(x => x.PersonalInfo!.DriverLicenseNumber)
                .Must(v => Regex.IsMatch(v!.Trim(), "^[0-9A-Za-z]{8,20}$"))
                .WithMessage("Driver licence number must be 8 to 20 letters or digits.")
                .When(x => !string.IsNullOrWhiteSpace(x.PersonalInfo!.DriverLicenseNumber));
        });
    }

    private static bool HasAnyPayload(ResubmitDriverApplicationCommand command) =>
        command.Items is { Count: > 0 }
        || command.Vehicle is { VehicleTypeId: not null }
        || command.Vehicle is { LicensePlate: not null }
        || command.Vehicle is { MaxLoadWeightKg: not null }
        || command.PersonalInfo is not null;
}

