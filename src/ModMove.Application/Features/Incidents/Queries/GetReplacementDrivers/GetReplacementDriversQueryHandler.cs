using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Matching;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Incidents.Queries.GetReplacementDrivers;

internal sealed class GetReplacementDriversQueryHandler(
    ISqlConnectionFactory sql,
    IPlatformSettings settings,
    TimeProvider clock) : IQueryHandler<GetReplacementDriversQuery, IReadOnlyList<ReplacementDriverResponse>>
{
    private const string IncidentContextSql = """
        SELECT
            i.id AS IncidentId,
            i.latitude AS IncidentLatitude,
            i.longitude AS IncidentLongitude,
            rep.role AS ReporterRole,
            cur.last_latitude AS DriverLatitude,
            cur.last_longitude AS DriverLongitude,
            o.id AS OrderId,
            o.driver_profile_id AS CurrentDriverProfileId,
            o.vehicle_type_id AS VehicleTypeId,
            o.pickup_latitude AS PickupLatitude,
            o.pickup_longitude AS PickupLongitude,
            o.total_fee AS TotalFee,
            o.payment_method AS PaymentMethod
        FROM in_transit_incidents i
        INNER JOIN shipping_orders o ON o.id = i.order_id
        INNER JOIN users rep ON rep.id = i.reported_by
        LEFT JOIN driver_profiles cur ON cur.id = o.driver_profile_id
        WHERE i.id = @IncidentId;
        """;

    private const string CandidateDriversSql = """
        SELECT
            dp.id AS DriverProfileId,
            u.id AS UserId,
            u.full_name AS FullName,
            u.phone_number AS PhoneNumber,
            v.license_plate AS LicensePlate,
            v.vehicle_type_id AS VehicleTypeId,
            vt.name AS VehicleTypeName,
            dp.last_latitude AS LastLatitude,
            dp.last_longitude AS LastLongitude,
            dp.last_location_at AS LastLocationAt,
            dw.wallet_status AS WalletStatus,
            COALESCE(dw.deposit_balance, 0) AS DepositBalance
        FROM driver_profiles dp
        INNER JOIN users u ON u.id = dp.user_id
        INNER JOIN vehicles v ON v.driver_profile_id = dp.id
        INNER JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
        LEFT JOIN driver_wallets dw ON dw.driver_profile_id = dp.id
        WHERE u.account_status = 'Active'
          AND dp.approval_status = 'Approved'
          AND dp.online_status = 'Online'
          AND dp.last_latitude IS NOT NULL
          AND dp.last_longitude IS NOT NULL
          AND dp.last_location_at > @FreshSince
          AND (@CurrentDriverProfileId IS NULL OR dp.id <> @CurrentDriverProfileId)
          AND v.vehicle_status = 'Active'
          AND v.vehicle_type_id = @VehicleTypeId
          AND NOT EXISTS (
              SELECT 1
              FROM shipping_orders ao
              WHERE ao.driver_profile_id = dp.id
                AND ao.order_status IN ('Accepted', 'Arrived', 'PickedUp', 'InTransit')
          );
        """;

    public async Task<IReadOnlyList<ReplacementDriverResponse>> Handle(
        GetReplacementDriversQuery request, CancellationToken cancellationToken)
    {
        using var conn = sql.CreateConnection();

        var ctx = await conn.QueryFirstOrDefaultAsync<IncidentContextRow>(
            new CommandDefinition(IncidentContextSql, new { request.IncidentId }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException(nameof(InTransitIncident), request.IncidentId);

        var radiusKm = (double)await settings.GetDecimalAsync(SettingKeys.MatchingRadiusKm, cancellationToken);
        var requiredDeposit = await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken);
        var isCash = string.Equals(ctx.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase);

        decimal requiredCommission = 0m;
        if (isCash)
        {
            var commissionRate = await settings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken);
            requiredCommission = Math.Round(commissionRate * ctx.TotalFee, 0, MidpointRounding.AwayFromZero);
        }

        var freshSince = (clock.GetUtcNow() - BusinessRules.GpsDisconnectAfter).UtcDateTime;
        var candidates = await conn.QueryAsync<CandidateDriverRow>(
            new CommandDefinition(
                CandidateDriversSql,
                new { FreshSince = freshSince, ctx.CurrentDriverProfileId, ctx.VehicleTypeId },
                cancellationToken: cancellationToken));

        // Same point as ReassignIncidentDriver: a customer's or a Dispatcher's incident is measured from the current driver's last GPS (7.2).
        var (refLat, refLon) = ctx.ReporterRole is "Customer" or "Dispatcher"
            ? IncidentHandlingRules.ReferencePoint(ctx.DriverLatitude, ctx.DriverLongitude, ctx.PickupLatitude, ctx.PickupLongitude)
            : IncidentHandlingRules.ReferencePoint(ctx.IncidentLatitude, ctx.IncidentLongitude, ctx.PickupLatitude, ctx.PickupLongitude);

        var results = new List<(double RawDistanceKm, ReplacementDriverResponse Dto)>();
        foreach (var c in candidates)
        {
            var walletActive = string.Equals(c.WalletStatus, "Active", StringComparison.OrdinalIgnoreCase);
            if (!walletActive || c.DepositBalance < requiredDeposit || (isCash && c.DepositBalance < requiredCommission))
                continue;

            var distKm = GeoDistance.HaversineKm(refLat, refLon, c.LastLatitude, c.LastLongitude);
            if (distKm > radiusKm)
                continue;

            results.Add((distKm, new ReplacementDriverResponse(
                c.DriverProfileId,
                c.UserId,
                c.FullName,
                c.PhoneNumber,
                c.LicensePlate,
                c.VehicleTypeId,
                c.VehicleTypeName,
                c.LastLatitude,
                c.LastLongitude,
                SqlHelpers.ToUtc(c.LastLocationAt),
                Math.Round(distKm, 2),
                c.DepositBalance)));
        }

        return results
            .OrderBy(x => x.RawDistanceKm)
            .ThenBy(x => x.Dto.DriverProfileId)
            .Select(x => x.Dto)
            .ToList();
    }

    private sealed class IncidentContextRow
    {
        public long IncidentId { get; init; }
        public decimal? IncidentLatitude { get; init; }
        public decimal? IncidentLongitude { get; init; }
        public string? ReporterRole { get; init; }
        public decimal? DriverLatitude { get; init; }
        public decimal? DriverLongitude { get; init; }
        public long OrderId { get; init; }
        public long? CurrentDriverProfileId { get; init; }
        public long VehicleTypeId { get; init; }
        public decimal PickupLatitude { get; init; }
        public decimal PickupLongitude { get; init; }
        public decimal TotalFee { get; init; }
        public string PaymentMethod { get; init; } = "";
    }

    private sealed class CandidateDriverRow
    {
        public long DriverProfileId { get; init; }
        public long UserId { get; init; }
        public string FullName { get; init; } = "";
        public string PhoneNumber { get; init; } = "";
        public string LicensePlate { get; init; } = "";
        public long VehicleTypeId { get; init; }
        public string VehicleTypeName { get; init; } = "";
        public decimal LastLatitude { get; init; }
        public decimal LastLongitude { get; init; }
        public DateTime LastLocationAt { get; init; }
        public string? WalletStatus { get; init; }
        public decimal DepositBalance { get; init; }
    }
}

