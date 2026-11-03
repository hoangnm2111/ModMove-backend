using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Drivers.Queries.GetDriverStatus;

/// <summary>
/// UC-DRV-08, driver home screen: what the signed-in driver needs to know before taking orders. One read of the
/// profile, vehicle, wallet and current order.
/// </summary>
public sealed record GetDriverStatusQuery : IQuery<DriverStatusResponse>;

/// <param name="HasProfile">False until the first application was submitted; every other field is then empty.</param>
/// <param name="ApprovalStatus">Pending, Approved or Rejected (GB-03).</param>
/// <param name="IsGpsStale">
/// DECISIONS 6.4 / GB-39: no GPS point yet, or none in the last 5 minutes. The driver is then shown no new orders.
/// </param>
/// <param name="ActiveOrderId">The order between Accepted and InTransit (GB-09 allows one), or null.</param>
/// <param name="CanReceiveOrders">
/// Approved, Online, fresh GPS and no order in progress (GB-03, GB-09, 6.4). The wallet and vehicle type checks that
/// depend on a particular order (GB-10, GB-16) are done when orders are listed and accepted.
/// </param>
public sealed record DriverStatusResponse(
    bool HasProfile,
    string? ApprovalStatus,
    bool IsOnline,
    long? VehicleTypeId,
    string? LicensePlate,
    decimal DepositBalance,
    decimal EarningsBalance,
    string? WalletStatus,
    long? ActiveOrderId,
    decimal? LastLatitude,
    decimal? LastLongitude,
    DateTimeOffset? LastLocationAt,
    bool IsGpsStale,
    bool CanReceiveOrders);

