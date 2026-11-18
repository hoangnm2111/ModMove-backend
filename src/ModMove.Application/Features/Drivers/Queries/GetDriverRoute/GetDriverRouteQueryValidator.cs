using FluentValidation;

namespace ModMove.Application.Features.Drivers.Queries.GetDriverRoute;

/// <summary>Nullable so a missing query parameter is a 400, not a route to (0, 0) that spends a MapVina call.</summary>
internal sealed class GetDriverRouteQueryValidator : AbstractValidator<GetDriverRouteQuery>
{
    public GetDriverRouteQueryValidator()
    {
        RuleFor(x => x.FromLatitude).NotNull().InclusiveBetween(-90, 90);
        RuleFor(x => x.ToLatitude).NotNull().InclusiveBetween(-90, 90);
        RuleFor(x => x.FromLongitude).NotNull().InclusiveBetween(-180, 180);
        RuleFor(x => x.ToLongitude).NotNull().InclusiveBetween(-180, 180);
    }
}
