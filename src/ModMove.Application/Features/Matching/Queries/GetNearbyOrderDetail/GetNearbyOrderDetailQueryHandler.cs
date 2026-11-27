using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;
using ModMove.Domain.Services;

namespace ModMove.Application.Features.Matching.Queries.GetNearbyOrderDetail;

internal sealed class GetNearbyOrderDetailQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IPlatformSettings settings,
    TimeProvider clock,
    IFileStorage storage) : IQueryHandler<GetNearbyOrderDetailQuery, NearbyOrderDetailResponse>
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

    private const string OrderDetailSql = """
        SELECT o.id AS OrderId,
               o.order_code AS OrderCode,
               o.order_status AS OrderStatus,
               o.driver_profile_id AS DriverProfileId,
               o.vehicle_type_id AS VehicleTypeId,
               vt.name AS VehicleTypeName,
               o.pickup_address AS PickupAddress,
               o.pickup_latitude AS PickupLatitude,
               o.pickup_longitude AS PickupLongitude,
               o.dropoff_address AS DropoffAddress,
               o.dropoff_latitude AS DropoffLatitude,
               o.dropoff_longitude AS DropoffLongitude,
               o.estimated_distance_km AS EstimatedDistanceKm,
               o.estimated_duration_min AS EstimatedDurationMin,
               o.distance_fee AS DistanceFee,
               o.service_fee AS ServiceFee,
               o.discount_amount AS DiscountAmount,
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
        INNER JOIN vehicle_types vt ON vt.id = o.vehicle_type_id
        LEFT JOIN master_data_items m ON m.category = 'CARGO_TYPE' AND m.code = cd.cargo_type
        WHERE o.id = @OrderId
        """;

    public async Task<NearbyOrderDetailResponse> Handle(
        GetNearbyOrderDetailQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.GetUtcNow();

        using var connection = connectionFactory.CreateConnection();
        var driver = await connection.QuerySingleOrDefaultAsync<DriverContextRow>(
            new CommandDefinition(DriverContextSql, new { UserId = userId }, cancellationToken: cancellationToken))
            ?? throw new UnauthorizedException("Sign in again.");

        if (driver.AccountStatus != nameof(AccountStatus.Active))
            throw CurrentUserExtensions.InactiveAccount();

        if (driver.DriverProfileId is null || driver.ApprovalStatus != nameof(ApprovalStatus.Approved))
            throw new DomainException("Driver documents must be approved first.", "DRIVER_NOT_APPROVED");

        var order = await connection.QuerySingleOrDefaultAsync<OrderDetailRow>(
            new CommandDefinition(OrderDetailSql, new { request.OrderId }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        // UC-DRV-08 7a, UC-DRV-09, GB-08: opening an order already taken by another driver returns 409 CONFLICT (MSG40).
        if (order.DriverProfileId is not null)
            throw new ConflictException("This order has already been accepted by another driver.");

        if (order.OrderStatus == nameof(OrderStatus.Cancelled))
            throw new DomainException("This order was cancelled and can no longer be accepted.", "ORDER_CANCELLED");

        if (order.OrderStatus != nameof(OrderStatus.Pending))
            throw new DomainException("This order is no longer available for matching.", "ORDER_NOT_AVAILABLE");

        var createdAt = SqlHelpers.ToUtc(order.CreatedAt);
        var paidAt = SqlHelpers.ToUtc(order.PaidAt);
        var isCash = string.Equals(order.PaymentMethod, nameof(PaymentMethod.Cash), StringComparison.OrdinalIgnoreCase);
        var isPayOs = string.Equals(order.PaymentMethod, nameof(PaymentMethod.PayOS), StringComparison.OrdinalIgnoreCase);

        DateTimeOffset matchingWindowStart;
        if (isCash)
        {
            matchingWindowStart = createdAt;
        }
        else if (isPayOs && order.PaymentStatus == nameof(PaymentStatus.Paid) && paidAt is { } paidTime)
        {
            matchingWindowStart = paidTime;
        }
        else
        {
            throw new DomainException("This order is no longer available for matching.", "ORDER_NOT_AVAILABLE");
        }

        var matchingExpiresAt = matchingWindowStart + BusinessRules.MatchingTimeout;
        if (now >= matchingExpiresAt)
            throw new DomainException("The matching window for this order has expired.", "ORDER_NOT_AVAILABLE");

        var radiusKm = (double)await settings.GetDecimalAsync(SettingKeys.MatchingRadiusKm, cancellationToken);
        var requiredDeposit = await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken);
        var commissionRate = await settings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken);

        var commission = ShippingOrder.CommissionFor(commissionRate, order.TotalFee);
        var driverEarning = order.TotalFee - commission;

        decimal? roundedDistanceKm = null;
        double? rawDistanceKm = null;
        if (driver.LastLatitude is { } driverLat && driver.LastLongitude is { } driverLon)
        {
            rawDistanceKm = GeoDistance.HaversineKm(driverLat, driverLon, order.PickupLatitude, order.PickupLongitude);
            roundedDistanceKm = Math.Round((decimal)rawDistanceKm.Value, 2, MidpointRounding.AwayFromZero);
        }

        var ineligibleReason = EvaluateEligibility(driver, order, now, isCash, commission, requiredDeposit, radiusKm, rawDistanceKm);

        return new NearbyOrderDetailResponse(
            order.OrderId,
            order.OrderCode,
            order.VehicleTypeId,
            order.VehicleTypeName,
            order.PickupAddress,
            order.PickupLatitude,
            order.PickupLongitude,
            order.DropoffAddress,
            order.DropoffLatitude,
            order.DropoffLongitude,
            roundedDistanceKm,
            order.EstimatedDistanceKm,
            order.EstimatedDurationMin,
            order.DistanceFee,
            order.ServiceFee,
            order.DiscountAmount,
            order.TotalFee,
            commission,
            driverEarning,
            order.PaymentMethod,
            order.PayerRole,
            order.HasLoadingService,
            order.CargoType,
            order.CargoTypeName,
            order.CargoQuantity,
            order.CargoWeightKg,
            order.CargoLengthCm,
            order.CargoWidthCm,
            order.CargoHeightCm,
            SqlHelpers.ParseJsonList(order.CargoPhotoUrlsJson).Select(storage.CreateViewUrl).ToList(),
            order.SpecialNotes,
            createdAt,
            matchingExpiresAt,
            CanAccept: ineligibleReason is null,
            IneligibleReason: ineligibleReason);
    }

    private static string? EvaluateEligibility(
        DriverContextRow driver,
        OrderDetailRow order,
        DateTimeOffset now,
        bool isCash,
        decimal commission,
        decimal requiredDeposit,
        double radiusKm,
        double? rawDistanceKm)
    {
        if (driver.OnlineStatus != nameof(OnlineStatus.Online))
            return "OFFLINE";

        if (driver.VehicleStatus != nameof(VehicleStatus.Active) || driver.VehicleTypeId != order.VehicleTypeId)
            return "VEHICLE_MISMATCH";

        if (driver.ActiveOrderId is not null)
            return "ACTIVE_TRIP";

        if (SqlHelpers.ToUtc(driver.LastLocationAt) is not { } lastGpsAt
            || now - lastGpsAt >= BusinessRules.GpsDisconnectAfter
            || rawDistanceKm is not { } distKm)
        {
            return "GPS_STALE";
        }

        if (distKm > radiusKm)
            return "OUT_OF_RADIUS";

        if (driver.WalletStatus != nameof(WalletStatus.Active)
            || driver.DepositBalance < requiredDeposit
            || (isCash && driver.DepositBalance < commission))
        {
            return "INSUFFICIENT_DEPOSIT";
        }

        return null;
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

    private sealed class OrderDetailRow
    {
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public string OrderStatus { get; init; } = "";
        public long? DriverProfileId { get; init; }
        public long VehicleTypeId { get; init; }
        public string VehicleTypeName { get; init; } = "";
        public string PickupAddress { get; init; } = "";
        public decimal PickupLatitude { get; init; }
        public decimal PickupLongitude { get; init; }
        public string DropoffAddress { get; init; } = "";
        public decimal DropoffLatitude { get; init; }
        public decimal DropoffLongitude { get; init; }
        public decimal EstimatedDistanceKm { get; init; }
        public int EstimatedDurationMin { get; init; }
        public decimal DistanceFee { get; init; }
        public decimal ServiceFee { get; init; }
        public decimal DiscountAmount { get; init; }
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

