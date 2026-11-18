using FluentValidation;

namespace ModMove.Application.Features.Drivers.Queries.GetDriverRoute;

internal sealed class GetDriverRouteQueryValidator : AbstractValidator<GetDriverRouteQuery>
{
    public GetDriverRouteQueryValidator()
    {
        RuleFor(x => x.FromLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.ToLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.FromLongitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.ToLongitude).InclusiveBetween(-180, 180);
    }
}
