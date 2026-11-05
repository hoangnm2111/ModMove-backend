using System.Linq.Expressions;
using Dapper;
using FluentValidation;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Orders;
using ModMove.Application.Features.Reports.Queries.GetRevenueReport;

namespace ModMove.Application.Features.Reports;

public sealed record CompletedOrderRevenueDto(
    long OrderId,
    string OrderCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset CompletedAt,
    string CustomerName,
    string CustomerPhone,
    string? DriverName,
    string? LicensePlate,
    string VehicleTypeName,
    decimal EstimatedDistanceKm,
    decimal DistanceFee,
    decimal ServiceFee,
    decimal DiscountAmount,
    decimal TotalFee,
    decimal CommissionRate,
    decimal CommissionAmount,
    decimal DriverEarning,
    string PaymentMethod,
    string PaymentStatus);

public sealed record DailyRevenueDto(
    DateOnly Date,
    int CompletedOrders,
    decimal Revenue,
    decimal TotalDeliveryFees,
    decimal LoadingFees,
    decimal DiscountAmount,
    decimal DriverEarnings);

internal static class RevenueReportData
{
    public const int MaxRangeDays = 366;
    private static readonly DateOnly MinDate = new(2020, 1, 1);
    private static readonly DateOnly MaxDate = new(2100, 12, 31);
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private const string OrdersSql = """
        SELECT o.id AS OrderId,
               o.order_code AS OrderCode,
               o.created_at AS CreatedAt,
               o.completed_at AS CompletedAt,
               cu.full_name AS CustomerName,
               cu.phone_number AS CustomerPhone,
               du.full_name AS DriverName,
               v.license_plate AS LicensePlate,
               vt.name AS VehicleTypeName,
               o.estimated_distance_km AS EstimatedDistanceKm,
               o.distance_fee AS DistanceFee,
               o.service_fee AS ServiceFee,
               o.discount_amount AS DiscountAmount,
               o.total_fee AS TotalFee,
               IFNULL(o.commission_rate, 0) AS CommissionRate,
               IFNULL(o.commission_amount, 0) AS CommissionAmount,
               o.payment_method AS PaymentMethod,
               o.payment_status AS PaymentStatus
        FROM shipping_orders o
        JOIN users cu ON cu.id = o.customer_id
        JOIN vehicle_types vt ON vt.id = o.vehicle_type_id
        LEFT JOIN driver_profiles dp ON dp.id = o.driver_profile_id
        LEFT JOIN users du ON du.id = dp.user_id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        WHERE o.order_status = 'Completed'
          AND o.completed_at IS NOT NULL
          AND (@FromUtc IS NULL OR o.completed_at >= @FromUtc)
          AND (@ToUtc IS NULL OR o.completed_at < @ToUtc)
        ORDER BY o.completed_at DESC, o.id DESC
        """;

    public static void AddDateRangeRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, DateOnly?>> fromExpr,
        Expression<Func<T, DateOnly?>> toExpr)
    {
        var getFrom = fromExpr.Compile();
        var getTo = toExpr.Compile();

        validator.RuleFor(fromExpr).InclusiveBetween(MinDate, MaxDate);
        validator.RuleFor(toExpr).InclusiveBetween(MinDate, MaxDate);

        validator.RuleFor(toExpr)
            .GreaterThanOrEqualTo(fromExpr)
            .When(x => getFrom(x) is not null && getTo(x) is not null)
            .WithMessage("'To Date' must not be before 'From Date'.");

        validator.RuleFor(toExpr)
            .Must((x, _) =>
            {
                var from = getFrom(x);
                var to = getTo(x);
                return from is null || to is null || to.Value.DayNumber - from.Value.DayNumber <= MaxRangeDays;
            })
            .When(x => getFrom(x) is not null && getTo(x) is not null && getTo(x) >= getFrom(x))
            .WithMessage($"The report date range must not exceed {MaxRangeDays} days.");
    }

    public static async Task<RevenueReportResponse> LoadAsync(
        ISqlConnectionFactory connectionFactory,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        var parameters = new
        {
            FromUtc = fromDate is { } from ? OrderCode.VietnamDayStartUtc(from).UtcDateTime : (DateTime?)null,
            ToUtc = toDate is { } to ? OrderCode.VietnamDayStartUtc(to.AddDays(1)).UtcDateTime : (DateTime?)null,
        };

        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(OrdersSql, parameters, cancellationToken: cancellationToken));

        var orders = rows.Select(r =>
        {
            var driverEarning = r.TotalFee - r.CommissionAmount;
            return new CompletedOrderRevenueDto(
                r.OrderId,
                r.OrderCode,
                SqlHelpers.ToUtc(r.CreatedAt),
                SqlHelpers.ToUtc(r.CompletedAt),
                r.CustomerName,
                r.CustomerPhone,
                r.DriverName,
                r.LicensePlate,
                r.VehicleTypeName,
                r.EstimatedDistanceKm,
                r.DistanceFee,
                r.ServiceFee,
                r.DiscountAmount,
                r.TotalFee,
                r.CommissionRate,
                r.CommissionAmount,
                driverEarning,
                r.PaymentMethod,
                r.PaymentStatus);
        }).ToList();

        var dailyStats = orders
            .GroupBy(o => ToVietnamDate(o.CompletedAt))
            .OrderBy(g => g.Key)
            .Select(g => new DailyRevenueDto(
                g.Key,
                g.Count(),
                g.Sum(o => o.CommissionAmount),
                g.Sum(o => o.DistanceFee),
                g.Sum(o => o.ServiceFee),
                g.Sum(o => o.DiscountAmount),
                g.Sum(o => o.DriverEarning)))
            .ToList();

        return new RevenueReportResponse(
            fromDate,
            toDate,
            orders.Count,
            orders.Sum(o => o.CommissionAmount),
            orders.Sum(o => o.DistanceFee),
            orders.Sum(o => o.ServiceFee),
            orders.Sum(o => o.DiscountAmount),
            orders.Sum(o => o.DriverEarning),
            dailyStats,
            orders);
    }

    public static DateOnly ToVietnamDate(DateTimeOffset utcTime)
    {
        var vnTime = utcTime.ToOffset(VietnamOffset);
        return new DateOnly(vnTime.Year, vnTime.Month, vnTime.Day);
    }

    public static DateTimeOffset ToVietnamTime(DateTimeOffset utcTime) =>
        utcTime.ToOffset(VietnamOffset);

    private sealed class Row
    {
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public DateTime CompletedAt { get; init; }
        public string CustomerName { get; init; } = "";
        public string CustomerPhone { get; init; } = "";
        public string? DriverName { get; init; }
        public string? LicensePlate { get; init; }
        public string VehicleTypeName { get; init; } = "";
        public decimal EstimatedDistanceKm { get; init; }
        public decimal DistanceFee { get; init; }
        public decimal ServiceFee { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal TotalFee { get; init; }
        public decimal CommissionRate { get; init; }
        public decimal CommissionAmount { get; init; }
        public string PaymentMethod { get; init; } = "";
        public string PaymentStatus { get; init; } = "";
    }
}

