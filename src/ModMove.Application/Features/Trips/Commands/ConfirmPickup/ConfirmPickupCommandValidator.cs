using FluentValidation;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Constants;

namespace ModMove.Application.Features.Trips.Commands.ConfirmPickup;

internal sealed class ConfirmPickupCommandValidator : AbstractValidator<ConfirmPickupCommand>
{
    public ConfirmPickupCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.PhotoUrls)
            .NotNull()
            .Must(urls => urls is { Count: >= BusinessRules.MinPickupPhotos and <= BusinessRules.MaxCargoPhotos })
            .WithMessage($"Between {BusinessRules.MinPickupPhotos} and {BusinessRules.MaxCargoPhotos} pickup photos are required.")
            .Must(urls => urls is null || urls.All(u => !string.IsNullOrWhiteSpace(u) && storage.IsOwnedUrl(u)))
            .WithMessage("Upload the photos through POST api/driver/uploads first.");
    }
}

