using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverEarnings;

public enum EarningsPeriod
{
    All,
    Today,
    Week,
    Month,
}

/// <summary>
/// UC-DRV-23, DECISIONS M1, 9.5: revenue summary and per-order earnings breakdown for completed trips of the signed-in driver.
/// Dates and presets use Vietnam time (<c>UTC+7</c>).
/// </summary>
public sealed record GetDriverEarningsQuery(
    EarningsPeriod Period = EarningsPeriod.All,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Page = 1,
    int PageSize = 20) : IQuery<DriverEarningsResponse>;

public sealed record DriverEarningsResponse(
    int CompletedTrips,
    decimal GrossFares,
    decimal TotalCommission,
    decimal NetEarnings,
    PagedResult<DriverOrderEarningItem> Orders);

public sealed record DriverOrderEarningItem(
    long OrderId,
    string OrderCode,
    string PickupAddress,
    string DropoffAddress,
    string PaymentMethod,
    decimal DistanceFee,
    decimal ServiceFee,
    decimal DiscountAmount,
    decimal TotalFee,
    decimal CommissionRate,
    decimal CommissionAmount,
    decimal DriverEarning,
    DateTimeOffset CompletedAt);

