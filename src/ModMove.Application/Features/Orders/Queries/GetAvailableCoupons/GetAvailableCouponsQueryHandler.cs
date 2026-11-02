using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Orders.Queries.GetAvailableCoupons;

internal sealed class GetAvailableCouponsQueryHandler(
    ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser, TimeProvider clock)
    : IQueryHandler<GetAvailableCouponsQuery, PagedResult<AvailableCouponResponse>>
{
    // Same conditions as Coupon.EnsureApplicable; the minimum order is checked when pricing, since the fare is not known here.
    private const string From = """
        FROM coupons
        WHERE is_active = 1 AND valid_from <= @Now AND expires_at >= @Now
          AND (usage_limit IS NULL OR used_count < usage_limit)
        """;

    private const string CountSql = $"SELECT COUNT(1) {From}";

    private const string ItemsSql = $"""
        SELECT discount_code AS DiscountCode, discount_value AS DiscountValue, minimum_order_value AS MinimumOrderValue,
               expires_at AS ExpiresAt, usage_limit - used_count AS RemainingUses
        {From}
        ORDER BY expires_at, id
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<AvailableCouponResponse>> Handle(GetAvailableCouponsQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        await CustomerAccess.EnsureActiveAsync(connection, customerId, cancellationToken);

        var parameters = new { Now = clock.GetUtcNow().UtcDateTime, request.PageSize, Offset = (request.Page - 1) * request.PageSize };
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new AvailableCouponResponse(
            r.DiscountCode, r.DiscountValue, r.MinimumOrderValue, CustomerAccess.Utc(r.ExpiresAt), r.RemainingUses)).ToList();
        return new PagedResult<AvailableCouponResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed class Row
    {
        public string DiscountCode { get; init; } = "";
        public decimal DiscountValue { get; init; }
        public decimal MinimumOrderValue { get; init; }
        public DateTime ExpiresAt { get; init; }
        public int? RemainingUses { get; init; }
    }
}
