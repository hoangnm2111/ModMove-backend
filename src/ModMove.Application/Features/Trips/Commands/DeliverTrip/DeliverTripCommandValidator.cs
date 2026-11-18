using FluentValidation;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Application.Features.Trips.Commands.DeliverTrip;

internal sealed class DeliverTripCommandValidator : AbstractValidator<DeliverTripCommand>
{
    public DeliverTripCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.DeliveryPhotoUrl)
            .NotEmpty()
            .Must(url => !string.IsNullOrWhiteSpace(url) && storage.IsOwnedUrl(url))
            .WithMessage("Upload the proof-of-delivery photo through POST api/driver/uploads first.");
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m)
            .When(x => x.Latitude is not null);
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m)
            .When(x => x.Longitude is not null);
        RuleFor(x => x)
            .Must(x => (x.Latitude is null) == (x.Longitude is null))
            .WithMessage("Provide both latitude and longitude or neither.");
    }
}

