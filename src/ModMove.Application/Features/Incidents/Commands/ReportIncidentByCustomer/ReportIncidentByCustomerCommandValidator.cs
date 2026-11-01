using FluentValidation;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Constants;

namespace ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;

internal sealed class ReportIncidentByCustomerCommandValidator : AbstractValidator<ReportIncidentByCustomerCommand>
{
    public ReportIncidentByCustomerCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.IncidentType).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000); // in_transit_incidents.description
        // No decision fixes a limit for incident photos yet; the refund evidence limit (7.4) is used until the team does.
        RuleFor(x => x.PhotoUrls)
            .Must(urls => urls is null || urls.Count <= BusinessRules.MaxRefundEvidencePhotos)
            .WithMessage($"At most {BusinessRules.MaxRefundEvidencePhotos} photos.")
            .Must(urls => urls is null || urls.All(u => !string.IsNullOrWhiteSpace(u) && storage.IsOwnedUrl(u)))
            .WithMessage("Upload the photos through POST api/customer/uploads first.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}
