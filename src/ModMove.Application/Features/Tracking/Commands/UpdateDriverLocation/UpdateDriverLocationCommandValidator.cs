using FluentValidation;

namespace ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;

internal sealed class UpdateDriverLocationCommandValidator : AbstractValidator<UpdateDriverLocationCommand>
{
    public UpdateDriverLocationCommandValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Accuracy).GreaterThanOrEqualTo(0).When(x => x.Accuracy is not null);
    }
}
