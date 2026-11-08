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
    }
}

