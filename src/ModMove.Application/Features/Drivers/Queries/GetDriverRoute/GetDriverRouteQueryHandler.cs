using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Drivers.Queries.GetDriverRoute;

internal sealed class GetDriverRouteQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IMapService maps) : IQueryHandler<GetDriverRouteQuery, DriverRouteResponse>
{
    private const string AccountSql = "SELECT account_status FROM users WHERE id = @UserId";

    public async Task<DriverRouteResponse> Handle(GetDriverRouteQuery request, CancellationToken cancellationToken)
    {
        // A token outlives an Admin lock by up to 15 minutes: refuse it here (B8) before spending the shared MapVina quota.
        using (var connection = connectionFactory.CreateConnection())
        {
            var status = await connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(AccountSql, new { UserId = currentUser.RequireUserId() }, cancellationToken: cancellationToken))
                ?? throw new UnauthorizedException("Sign in again.");
            if (status != nameof(AccountStatus.Active))
                throw CurrentUserExtensions.InactiveAccount();
        }

        // The validator guarantees all four values are present.
        var route = await maps.GetRouteAsync(
            new GeoPoint(request.FromLatitude!.Value, request.FromLongitude!.Value),
            new GeoPoint(request.ToLatitude!.Value, request.ToLongitude!.Value),
            cancellationToken);
        return new DriverRouteResponse(route.DistanceKm, route.DurationMinutes, route.Polyline);
    }
}
