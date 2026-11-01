using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Incidents;

namespace ModMove.Application.Features.Monitoring.Queries.ListOnlineDrivers;

internal sealed class ListOnlineDriversQueryHandler(
    ISqlConnectionFactory connectionFactory,
    TimeProvider clock) : IQueryHandler<ListOnlineDriversQuery, IReadOnlyList<OnlineDriverMapItemResponse>>
{
    private const string Sql = """
        SELECT dp.id AS DriverProfileId,
               u.id AS UserId,
               u.full_name AS FullName,
               u.phone_number AS PhoneNumber,
               v.license_plate AS LicensePlate,
               v.vehicle_type_id AS VehicleTypeId,
               vt.name AS VehicleTypeName,
               dp.last_latitude AS Latitude,
               dp.last_longitude AS Longitude,
               dp.last_location_at AS LastLocationAt,
               ao.id AS ActiveOrderId,
               ao.order_code AS ActiveOrderCode,
               ao.order_status AS ActiveOrderStatus
        FROM driver_profiles dp
        INNER JOIN users u ON u.id = dp.user_id
        INNER JOIN vehicles v ON v.driver_profile_id = dp.id
        INNER JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
        LEFT JOIN shipping_orders ao
               ON ao.id = (
                   SELECT o2.id
                   FROM shipping_orders o2
                   WHERE o2.driver_profile_id = dp.id
                     AND o2.order_status IN ('Accepted', 'Arrived', 'PickedUp', 'InTransit')
                   ORDER BY o2.id DESC
                   LIMIT 1
               )
        WHERE u.account_status = 'Active'
          AND dp.approval_status = 'Approved'
          AND dp.online_status = 'Online'
          AND v.vehicle_status = 'Active'
          AND dp.last_latitude IS NOT NULL
          AND dp.last_longitude IS NOT NULL
          AND dp.last_location_at >= @FreshSince
        ORDER BY dp.id ASC;
        """;

    public async Task<IReadOnlyList<OnlineDriverMapItemResponse>> Handle(
        ListOnlineDriversQuery request, CancellationToken cancellationToken)
    {
        var freshSince = (clock.GetUtcNow() - IncidentHandlingRules.GpsFreshWindow).UtcDateTime;

        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(Sql, new { FreshSince = freshSince }, cancellationToken: cancellationToken));

        return rows.Select(r => new OnlineDriverMapItemResponse(
            r.DriverProfileId,
            r.UserId,
            r.FullName,
            r.PhoneNumber,
            r.LicensePlate,
            r.VehicleTypeId,
            r.VehicleTypeName,
            r.Latitude,
            r.Longitude,
            SqlHelpers.ToUtc(r.LastLocationAt),
            r.ActiveOrderId,
            r.ActiveOrderCode,
            r.ActiveOrderStatus)).ToList();
    }

    private sealed class Row
    {
        public long DriverProfileId { get; init; }
        public long UserId { get; init; }
        public string FullName { get; init; } = "";
        public string PhoneNumber { get; init; } = "";
        public string LicensePlate { get; init; } = "";
        public long VehicleTypeId { get; init; }
        public string VehicleTypeName { get; init; } = "";
        public decimal Latitude { get; init; }
        public decimal Longitude { get; init; }
        public DateTime LastLocationAt { get; init; }
        public long? ActiveOrderId { get; init; }
        public string? ActiveOrderCode { get; init; }
        public string? ActiveOrderStatus { get; init; }
    }
}

