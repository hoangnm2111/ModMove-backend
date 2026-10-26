using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.MasterData.Coupons.Queries.GetCoupons;

internal sealed class GetCouponsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetCouponsQuery, PagedResult<CouponDto>>
{
    private const string CountSql = """
        SELECT COUNT(1)
        FROM coupons
        WHERE (@IsActive IS NULL OR is_active = @IsActive)
        """;

    private const string ItemsSql = """
        SELECT id AS Id, discount_code AS DiscountCode, discount_value AS DiscountValue,
               minimum_order_value AS MinimumOrderValue, valid_from AS ValidFrom,
               expires_at AS ExpiresAt, usage_limit AS UsageLimit, used_count AS UsedCount,
               is_active AS IsActive, created_at AS CreatedAt
        FROM coupons
        WHERE (@IsActive IS NULL OR is_active = @IsActive)
        ORDER BY id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<CouponDto>> Handle(GetCouponsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        using var connection = connectionFactory.CreateConnection();
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(CountSql, new { request.IsActive }, cancellationToken: cancellationToken));

        var items = await connection.QueryAsync<CouponDto>(
            new CommandDefinition(ItemsSql, new { request.IsActive, PageSize = pageSize, Offset = offset }, cancellationToken: cancellationToken));

        return new PagedResult<CouponDto>(items.AsList(), page, pageSize, totalCount);
    }
}
