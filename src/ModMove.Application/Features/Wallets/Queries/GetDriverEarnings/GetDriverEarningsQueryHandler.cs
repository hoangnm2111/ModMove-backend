using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Orders;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverEarnings;

internal sealed class GetDriverEarningsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    TimeProvider clock) : IQueryHandler<GetDriverEarningsQuery, DriverEarningsResponse>
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private const string FilterSql = """
        WHERE o.driver_profile_id = @DriverProfileId
          AND o.order_status = 'Completed'
          AND (@FromUtc IS NULL OR COALESCE(o.completed_at, o.created_at) >= @FromUtc)
          AND (@ToUtc IS NULL OR COALESCE(o.completed_at, o.created_at) < @ToUtc)
        """;

    private const string SummarySql = $"""
        SELECT COUNT(1) AS CompletedTrips,
               COALESCE(SUM(o.total_fee), 0) AS GrossFares,
               COALESCE(SUM(COALESCE(o.commission_amount, 0)), 0) AS TotalCommission,
               COALESCE(SUM(o.total_fee - COALESCE(o.commission_amount, 0)), 0) AS NetEarnings
        FROM shipping_orders o
        {FilterSql}
        """;

    private const string ItemsSql = $"""
        SELECT o.id AS OrderId,
               o.order_code AS OrderCode,
               o.pickup_address AS PickupAddress,
               o.dropoff_address AS DropoffAddress,
               o.payment_method AS PaymentMethod,
               o.distance_fee AS DistanceFee,
               o.service_fee AS ServiceFee,
               o.discount_amount AS DiscountAmount,
               o.total_fee AS TotalFee,
               COALESCE(o.commission_rate, 0) AS CommissionRate,
               COALESCE(o.commission_amount, 0) AS CommissionAmount,
               o.total_fee - COALESCE(o.commission_amount, 0) AS DriverEarning,
               COALESCE(o.completed_at, o.created_at) AS CompletedAt
        FROM shipping_orders o
        {FilterSql}
        ORDER BY COALESCE(o.completed_at, o.created_at) DESC, o.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<DriverEarningsResponse> Handle(
        GetDriverEarningsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var context = await DriverAccess.LoadApprovedReadContextAsync(connection, userId, cancellationToken);

        var (fromUtc, toUtc) = ResolveRangeUtc(request, clock.GetUtcNow());
        var parameters = new
        {
            DriverProfileId = context.DriverProfileId!.Value,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        var summary = await connection.QuerySingleAsync<SummaryRow>(
            new CommandDefinition(SummarySql, parameters, cancellationToken: cancellationToken));

        if (summary.CompletedTrips == 0)
        {
            return new DriverEarningsResponse(
                0, 0m, 0m, 0m,
                new PagedResult<DriverOrderEarningItem>([], request.Page, request.PageSize, 0));
        }

        var rows = await connection.QueryAsync<ItemRow>(
            new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new DriverOrderEarningItem(
            r.OrderId,
            r.OrderCode,
            r.PickupAddress,
            r.DropoffAddress,
            r.PaymentMethod,
            r.DistanceFee,
            r.ServiceFee,
            r.DiscountAmount,
            r.TotalFee,
            r.CommissionRate,
            r.CommissionAmount,
            r.DriverEarning,
            SqlHelpers.ToUtc(r.CompletedAt))).ToList();

        return new DriverEarningsResponse(
            summary.CompletedTrips,
            summary.GrossFares,
            summary.TotalCommission,
            summary.NetEarnings,
            new PagedResult<DriverOrderEarningItem>(items, request.Page, request.PageSize, summary.CompletedTrips));
    }

    private static (DateTime? FromUtc, DateTime? ToUtc) ResolveRangeUtc(GetDriverEarningsQuery request, DateTimeOffset nowUtc)
    {
        if (request.FromDate is not null || request.ToDate is not null)
        {
            var from = request.FromDate is { } f ? OrderCode.VietnamDayStartUtc(f).UtcDateTime : (DateTime?)null;
            var to = request.ToDate is { } t ? OrderCode.VietnamDayStartUtc(t.AddDays(1)).UtcDateTime : (DateTime?)null;
            return (from, to);
        }

        if (request.Period == EarningsPeriod.All)
            return (null, null);

        var vnNow = nowUtc.ToOffset(VietnamOffset);
        var vnToday = new DateOnly(vnNow.Year, vnNow.Month, vnNow.Day);
        var endExclusive = OrderCode.VietnamDayStartUtc(vnToday.AddDays(1)).UtcDateTime;

        var startDay = request.Period switch
        {
            EarningsPeriod.Today => vnToday,
            EarningsPeriod.Week => vnToday.AddDays(-(((int)vnToday.DayOfWeek + 6) % 7)), // Monday = 0
            EarningsPeriod.Month => new DateOnly(vnToday.Year, vnToday.Month, 1),
            _ => vnToday,
        };

        return (OrderCode.VietnamDayStartUtc(startDay).UtcDateTime, endExclusive);
    }

    private sealed class SummaryRow
    {
        public int CompletedTrips { get; init; }
        public decimal GrossFares { get; init; }
        public decimal TotalCommission { get; init; }
        public decimal NetEarnings { get; init; }
    }

    private sealed class ItemRow
    {
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public string PickupAddress { get; init; } = "";
        public string DropoffAddress { get; init; } = "";
        public string PaymentMethod { get; init; } = "";
        public decimal DistanceFee { get; init; }
        public decimal ServiceFee { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal TotalFee { get; init; }
        public decimal CommissionRate { get; init; }
        public decimal CommissionAmount { get; init; }
        public decimal DriverEarning { get; init; }
        public DateTime CompletedAt { get; init; }
    }
}

