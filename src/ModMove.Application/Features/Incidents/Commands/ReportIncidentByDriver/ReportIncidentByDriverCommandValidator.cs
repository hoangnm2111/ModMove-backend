using FluentValidation;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Constants;

namespace ModMove.Application.Features.Incidents.Commands.ReportIncidentByDriver;

internal sealed class ReportIncidentByDriverCommandValidator : AbstractValidator<ReportIncidentByDriverCommand>
{
    public ReportIncidentByDriverCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.IncidentType).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.PhotoUrls)
            .Must(urls => urls is null || urls.Count <= BusinessRules.MaxRefundEvidencePhotos)
            .WithMessage($"At most {BusinessRules.MaxRefundEvidencePhotos} photos.")
            .Must(urls => urls is null || urls.All(u => !string.IsNullOrWhiteSpace(u) && storage.IsOwnedUrl(u)))
            .WithMessage("Upload the photos through POST api/driver/uploads first.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x)
            .Must(x => (x.Latitude is null) == (x.Longitude is null))
            .WithMessage("Latitude and longitude must be provided together.");
    }
}

