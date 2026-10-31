using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Orders.Queries;

namespace ModMove.Application.Features.Ratings.Queries.GetMyRatings;

internal sealed class GetMyRatingsQueryHandler(ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser)
    : IQueryHandler<GetMyRatingsQuery, PagedResult<MyRatingResponse>>
{
    private const string CountSql = "SELECT COUNT(1) FROM ratings r WHERE r.customer_id = @CustomerId";

    private const string ItemsSql = """
        SELECT r.id AS Id, r.order_id AS OrderId, o.order_code AS OrderCode, r.rating_stars AS RatingStars,
               r.review_comment AS ReviewComment, du.full_name AS DriverName, r.created_at AS CreatedAt
        FROM ratings r
        JOIN shipping_orders o ON o.id = r.order_id
        JOIN driver_profiles dp ON dp.id = r.driver_profile_id
        JOIN users du ON du.id = dp.user_id
        WHERE r.customer_id = @CustomerId
        ORDER BY r.created_at DESC, r.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<MyRatingResponse>> Handle(GetMyRatingsQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        await CustomerAccess.EnsureActiveAsync(connection, customerId, cancellationToken);

        var parameters = new { CustomerId = customerId, request.PageSize, Offset = (request.Page - 1) * request.PageSize };
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new MyRatingResponse(
            r.Id, r.OrderId, r.OrderCode, r.RatingStars, r.ReviewComment, r.DriverName, CustomerAccess.Utc(r.CreatedAt))).ToList();
        return new PagedResult<MyRatingResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public int RatingStars { get; init; }
        public string? ReviewComment { get; init; }
        public string DriverName { get; init; } = "";
        public DateTime CreatedAt { get; init; }
    }
}
