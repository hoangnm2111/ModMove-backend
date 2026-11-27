using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Services;

namespace ModMove.Application.Features.Matching.Queries.GetNearbyOrders;

internal sealed class GetNearbyOrdersQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IPlatformSettings settings,
    TimeProvider clock,
    IFileStorage storage) : IQueryHandler<GetNearbyOrdersQuery, IReadOnlyList<NearbyOrderDto>>
{
    private const string DriverContextSql = """
        SELECT u.account_status AS AccountStatus,
               dp.id AS DriverProfileId,
               dp.approval_status AS ApprovalStatus,
               dp.online_status AS OnlineStatus,
               dp.last_latitude AS LastLatitude,
               dp.last_longitude AS LastLongitude,
               dp.last_location_at AS LastLocationAt,
               v.vehicle_type_id AS VehicleTypeId,
               v.vehicle_status AS VehicleStatus,
               w.wallet_status AS WalletStatus,
               COALESCE(w.deposit_balance, 0) AS DepositBalance,
               (SELECT ao.id
                FROM shipping_orders ao
                WHERE ao.driver_profile_id = dp.id
                  AND ao.order_status IN ('Accepted', 'Arrived', 'PickedUp', 'InTransit')
                ORDER BY ao.id DESC
                LIMIT 1) AS ActiveOrderId
        FROM users u
        LEFT JOIN driver_profiles dp ON dp.user_id = u.id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        LEFT JOIN driver_wallets w ON w.driver_profile_id = dp.id
        WHERE u.id = @UserId
        """;

    private const string CandidateOrdersSql = """
        SELECT o.id AS OrderId,
               o.order_code AS OrderCode,
               o.order_status AS OrderStatus,
               o.vehicle_type_id AS VehicleTypeId,
               o.pickup_address AS PickupAddress,
               o.pickup_latitude AS PickupLatitude,
               o.pickup_longitude AS PickupLongitude,
               o.dropoff_address AS DropoffAddress,
               o.dropoff_latitude AS DropoffLatitude,
               o.dropoff_longitude AS DropoffLongitude,
               o.estimated_distance_km AS EstimatedDistanceKm,
               o.estimated_duration_min AS EstimatedDurationMin,
               o.total_fee AS TotalFee,
               o.payment_method AS PaymentMethod,
               o.payment_status AS PaymentStatus,
               o.payer_role AS PayerRole,
               o.has_loading_service AS HasLoadingService,
               o.created_at AS CreatedAt,
               o.paid_at AS PaidAt,
               cd.cargo_type AS CargoType,
               m.name AS CargoTypeName,
               cd.quantity AS CargoQuantity,
               cd.weight_kg AS CargoWeightKg,
               cd.length_cm AS CargoLengthCm,
               cd.width_cm AS CargoWidthCm,
               cd.height_cm AS CargoHeightCm,
               cd.photo_urls AS CargoPhotoUrlsJson,
               cd.special_notes AS SpecialNotes
        FROM shipping_orders o
        INNER JOIN cargo_details cd ON cd.order_id = o.id
        LEFT JOIN master_data_items m ON m.category = 'CARGO_TYPE' AND m.code = cd.cargo_type
        WHERE o.order_status = 'Pending'
          AND o.vehicle_type_id = @VehicleTypeId
          AND (
              (o.payment_method = 'Cash' AND o.created_at > @MatchValidAfter)
              OR (o.payment_method = 'PayOS' AND o.payment_status = 'Paid' AND o.paid_at IS NOT NULL AND o.paid_at > @MatchValidAfter)
          )
        ORDER BY o.id ASC
        """;

    public async Task<IReadOnlyList<NearbyOrderDto>> Handle(
        GetNearbyOrdersQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.GetUtcNow();

        using var connection = connectionFactory.CreateConnection();
        var driver = await connection.QuerySingleOrDefaultAsync<DriverContextRow>(
            new CommandDefinition(DriverContextSql, new { UserId = userId }, cancellationToken: cancellationToken))
            ?? throw new UnauthorizedException("Sign in again.");

        if (driver.AccountStatus != nameof(AccountStatus.Active))
            throw CurrentUserExtensions.InactiveAccount();

        // B5, GB-03, GB-09, DECISIONS 6.4: only an Approved, Online driver with an Active vehicle, no running trip,
        // and a GPS point younger than 5 minutes sees open orders.
        if (driver.DriverProfileId is null
            || driver.ApprovalStatus != nameof(ApprovalStatus.Approved)
            || driver.OnlineStatus != nameof(OnlineStatus.Online)
            || driver.VehicleStatus != nameof(VehicleStatus.Active)
            || driver.VehicleTypeId is not { } driverVehicleTypeId
            || driver.ActiveOrderId is not null
            || driver.LastLatitude is not { } driverLat
            || driver.LastLongitude is not { } driverLon
            || SqlHelpers.ToUtc(driver.LastLocationAt) is not { } lastGpsAt
            || now - lastGpsAt >= BusinessRules.GpsDisconnectAfter)
        {
            return [];
        }

        var radiusKm = (double)await settings.GetDecimalAsync(SettingKeys.MatchingRadiusKm, cancellationToken);
        var requiredDeposit = await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken);
        var commissionRate = await settings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken);
        var walletActive = driver.WalletStatus == nameof(WalletStatus.Active);

        var matchValidAfter = (now - BusinessRules.MatchingTimeout).UtcDateTime;
        var candidates = await connection.QueryAsync<CandidateOrderRow>(
            new CommandDefinition(
                CandidateOrdersSql,
                new { VehicleTypeId = driverVehicleTypeId, MatchValidAfter = matchValidAfter },
                cancellationToken: cancellationToken));

        var matches = new List<(double RawDistanceKm, NearbyOrderDto Dto)>();
        foreach (var row in candidates)
        {
            if (row.OrderStatus != nameof(OrderStatus.Pending) || row.VehicleTypeId != driverVehicleTypeId)
                continue;

            var createdAt = SqlHelpers.ToUtc(row.CreatedAt);
            var paidAt = SqlHelpers.ToUtc(row.PaidAt);
            var isCash = string.Equals(row.PaymentMethod, nameof(PaymentMethod.Cash), StringComparison.OrdinalIgnoreCase);
            var isPayOs = string.Equals(row.PaymentMethod, nameof(PaymentMethod.PayOS), StringComparison.OrdinalIgnoreCase);

            // B10, GB-24, GB-31, DECISIONS 3.2: cash orders wait 10 minutes from creation; PayOS orders are shown
            // only once Paid and wait 10 minutes from paid_at.
            if (isCash)
            {
                if (now - createdAt >= BusinessRules.MatchingTimeout)
                    continue;
            }
            else if (isPayOs)
            {
                if (row.PaymentStatus != nameof(PaymentStatus.Paid)
                    || paidAt is not { } paidTime
                    || now - paidTime >= BusinessRules.MatchingTimeout)
                {
                    continue;
                }
            }
            else
            {
                continue;
            }

            // GB-14, DECISIONS M1: commission = round(rate * total_fee), driver earns total_fee - commission.
            var commission = ShippingOrder.CommissionFor(commissionRate, row.TotalFee);
            var driverEarning = row.TotalFee - commission;

            // GB-16: all orders require an Active wallet with DepositBalance >= REQUIRED_DEPOSIT; cash orders also require >= this order's commission.
            if (!walletActive || driver.DepositBalance < requiredDeposit || (isCash && driver.DepositBalance < commission))
                continue;

            var distKm = GeoDistance.HaversineKm(driverLat, driverLon, row.PickupLatitude, row.PickupLongitude);
            if (distKm > radiusKm)
                continue;

            var roundedDistanceKm = Math.Round((decimal)distKm, 2, MidpointRounding.AwayFromZero);

            matches.Add((distKm, new NearbyOrderDto(
                row.OrderId,
                row.OrderCode,
                row.PickupAddress,
                row.PickupLatitude,
                row.PickupLongitude,
                row.DropoffAddress,
                row.DropoffLatitude,
                row.DropoffLongitude,
                roundedDistanceKm,
                row.EstimatedDistanceKm,
                row.EstimatedDurationMin,
                row.TotalFee,
                commission,
                driverEarning,
                row.PaymentMethod,
                row.PayerRole,
                row.HasLoadingService,
                row.CargoType,
                row.CargoTypeName,
                row.CargoQuantity,
                row.CargoWeightKg,
                row.CargoLengthCm,
                row.CargoWidthCm,
                row.CargoHeightCm,
                SqlHelpers.ParseJsonList(row.CargoPhotoUrlsJson).Select(storage.CreateViewUrl).ToList(),
                row.SpecialNotes,
                createdAt)));
        }

        return matches
            .OrderBy(m => m.RawDistanceKm)
            .ThenBy(m => m.Dto.OrderId)
            .Select(m => m.Dto)
            .ToList();
    }

    private sealed class DriverContextRow
    {
        public string AccountStatus { get; init; } = "";
        public long? DriverProfileId { get; init; }
        public string? ApprovalStatus { get; init; }
        public string? OnlineStatus { get; init; }
        public decimal? LastLatitude { get; init; }
        public decimal? LastLongitude { get; init; }
        public DateTime? LastLocationAt { get; init; }
        public long? VehicleTypeId { get; init; }
        public string? VehicleStatus { get; init; }
        public string? WalletStatus { get; init; }
        public decimal DepositBalance { get; init; }
        public long? ActiveOrderId { get; init; }
    }

    private sealed class CandidateOrderRow
    {
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public string OrderStatus { get; init; } = "Pending";
        public long VehicleTypeId { get; init; }
        public string PickupAddress { get; init; } = "";
        public decimal PickupLatitude { get; init; }
        public decimal PickupLongitude { get; init; }
        public string DropoffAddress { get; init; } = "";
        public decimal DropoffLatitude { get; init; }
        public decimal DropoffLongitude { get; init; }
        public decimal EstimatedDistanceKm { get; init; }
        public int EstimatedDurationMin { get; init; }
        public decimal TotalFee { get; init; }
        public string PaymentMethod { get; init; } = "";
        public string PaymentStatus { get; init; } = "";
        public string? PayerRole { get; init; }
        public bool HasLoadingService { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? PaidAt { get; init; }
        public string CargoType { get; init; } = "";
        public string? CargoTypeName { get; init; }
        public int CargoQuantity { get; init; }
        public decimal CargoWeightKg { get; init; }
        public int CargoLengthCm { get; init; }
        public int CargoWidthCm { get; init; }
        public int CargoHeightCm { get; init; }
        public string? CargoPhotoUrlsJson { get; init; }
        public string? SpecialNotes { get; init; }
    }
}

