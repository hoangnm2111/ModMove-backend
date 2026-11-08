using FluentValidation;

namespace ModMove.Application.Features.Trips.Queries.GetDriverTripDetail;

internal sealed class GetDriverTripDetailQueryValidator : AbstractValidator<GetDriverTripDetailQuery>
{
    public GetDriverTripDetailQueryValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
    }
}

