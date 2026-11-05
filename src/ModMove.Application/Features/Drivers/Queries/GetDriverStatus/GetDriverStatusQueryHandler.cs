using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Constants;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Drivers.Queries.GetDriverStatus;

internal sealed class GetDriverStatusQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    TimeProvider clock) : IQueryHandler<GetDriverStatusQuery, DriverStatusResponse>
{
    // One row per user. The account status comes with it, so a locked or deleted driver whose token is still valid is
    // refused (B8) without a second round trip. LEFT JOINs: a driver who never applied has no profile, vehicle or wallet.
    private const string Sql = """
        SELECT u.account_status AS AccountStatus,
               dp.id AS DriverProfileId,
               dp.approval_status AS ApprovalStatus,
               dp.online_status AS OnlineStatus,
               dp.last_latitude AS LastLatitude,
               dp.last_longitude AS LastLongitude,
               dp.last_location_at AS LastLocationAt,
               v.vehicle_type_id AS VehicleTypeId,
               v.license_plate AS LicensePlate,
               w.deposit_balance AS DepositBalance,
               w.earnings_balance AS EarningsBalance,
               w.wallet_status AS WalletStatus,
               (SELECT o.id
                FROM shipping_orders o
                WHERE o.driver_profile_id = dp.id
                  AND o.order_status IN ('Accepted', 'Arrived', 'PickedUp', 'InTransit')
                ORDER BY o.id DESC
                LIMIT 1) AS ActiveOrderId
        FROM users u
        LEFT JOIN driver_profiles dp ON dp.user_id = u.id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        LEFT JOIN driver_wallets w ON w.driver_profile_id = dp.id
        WHERE u.id = @UserId
        """;

    public async Task<DriverStatusResponse> Handle(GetDriverStatusQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(Sql, new { UserId = userId }, cancellationToken: cancellationToken))
            ?? throw new UnauthorizedException("Sign in again.");

        if (row.AccountStatus != nameof(AccountStatus.Active))
            throw CurrentUserExtensions.InactiveAccount();

        if (row.DriverProfileId is null)
        {
            return new DriverStatusResponse(
                HasProfile: false, ApprovalStatus: null, IsOnline: false, VehicleTypeId: null, LicensePlate: null,
                DepositBalance: 0, EarningsBalance: 0, WalletStatus: null, ActiveOrderId: null,
                LastLatitude: null, LastLongitude: null, LastLocationAt: null, IsGpsStale: true, CanReceiveOrders: false);
        }

        var lastLocationAt = SqlHelpers.ToUtc(row.LastLocationAt);
        var isGpsStale = lastLocationAt is not { } at || clock.GetUtcNow() - at >= BusinessRules.GpsDisconnectAfter;
        var isOnline = row.OnlineStatus == nameof(OnlineStatus.Online);
        var canReceiveOrders = row.ApprovalStatus == nameof(ApprovalStatus.Approved)
            && isOnline && !isGpsStale && row.ActiveOrderId is null;

        return new DriverStatusResponse(
            HasProfile: true,
            row.ApprovalStatus,
            isOnline,
            row.VehicleTypeId,
            row.LicensePlate,
            row.DepositBalance ?? 0,
            row.EarningsBalance ?? 0,
            row.WalletStatus,
            row.ActiveOrderId,
            row.LastLatitude,
            row.LastLongitude,
            lastLocationAt,
            isGpsStale,
            canReceiveOrders);
    }

    private sealed class Row
    {
        public string AccountStatus { get; init; } = "";
        public long? DriverProfileId { get; init; }
        public string? ApprovalStatus { get; init; }
        public string? OnlineStatus { get; init; }
        public decimal? LastLatitude { get; init; }
        public decimal? LastLongitude { get; init; }
        public DateTime? LastLocationAt { get; init; }
        public long? VehicleTypeId { get; init; }
        public string? LicensePlate { get; init; }
        public decimal? DepositBalance { get; init; }
        public decimal? EarningsBalance { get; init; }
        public string? WalletStatus { get; init; }
        public long? ActiveOrderId { get; init; }
    }
}

