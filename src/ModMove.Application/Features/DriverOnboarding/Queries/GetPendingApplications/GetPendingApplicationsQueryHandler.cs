using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.DriverOnboarding.Queries.GetPendingApplications;

internal sealed class GetPendingApplicationsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetPendingApplicationsQuery, PagedResult<PendingApplicationDto>>
{
    private const string CountSql = """
        SELECT COUNT(1)
        FROM driver_applications da
        WHERE da.application_status = 'Pending'
        """;

    private const string ItemsSql = """
        SELECT
            da.id AS ApplicationId,
            da.driver_profile_id AS DriverProfileId,
            u.full_name AS FullName,
            u.phone_number AS PhoneNumber,
            vt.name AS VehicleTypeName,
            da.license_plate AS LicensePlate,
            da.version_number AS VersionNumber,
            da.submitted_at AS SubmittedAt
        FROM driver_applications da
        JOIN driver_profiles dp ON dp.id = da.driver_profile_id
        JOIN users u ON u.id = dp.user_id
        JOIN vehicle_types vt ON vt.id = da.vehicle_type_id
        WHERE da.application_status = 'Pending'
        ORDER BY da.submitted_at ASC, da.id ASC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<PendingApplicationDto>> Handle(GetPendingApplicationsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        using var connection = connectionFactory.CreateConnection();
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(CountSql, cancellationToken: cancellationToken));

        var items = await connection.QueryAsync<PendingApplicationDto>(
            new CommandDefinition(ItemsSql, new { PageSize = pageSize, Offset = offset }, cancellationToken: cancellationToken));

        return new PagedResult<PendingApplicationDto>(items.AsList(), page, pageSize, totalCount);
    }
}
