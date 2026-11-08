using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Ratings.Queries.GetDriverRatings;

internal sealed class GetDriverRatingsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser) : IQueryHandler<GetDriverRatingsQuery, DriverRatingsResponse>
{
    private const string SummarySql = """
        SELECT COUNT(1) AS TotalRatings,
               AVG(CAST(r.rating_stars AS DECIMAL(5,2))) AS AverageStars,
               COALESCE(SUM(CASE WHEN r.rating_stars = 5 THEN 1 ELSE 0 END), 0) AS FiveStarCount,
               COALESCE(SUM(CASE WHEN r.rating_stars = 4 THEN 1 ELSE 0 END), 0) AS FourStarCount,
               COALESCE(SUM(CASE WHEN r.rating_stars = 3 THEN 1 ELSE 0 END), 0) AS ThreeStarCount,
               COALESCE(SUM(CASE WHEN r.rating_stars = 2 THEN 1 ELSE 0 END), 0) AS TwoStarCount,
               COALESCE(SUM(CASE WHEN r.rating_stars = 1 THEN 1 ELSE 0 END), 0) AS OneStarCount
        FROM ratings r
        WHERE r.driver_profile_id = @DriverProfileId
        """;

    private const string FilteredCountSql = """
        SELECT COUNT(1)
        FROM ratings r
        WHERE r.driver_profile_id = @DriverProfileId
          AND (@Stars IS NULL OR r.rating_stars = @Stars)
        """;

    private const string ItemsSql = """
        SELECT r.id AS Id,
               r.order_id AS OrderId,
               o.order_code AS OrderCode,
               r.rating_stars AS RatingStars,
               r.review_comment AS ReviewComment,
               cu.full_name AS CustomerName,
               r.created_at AS CreatedAt
        FROM ratings r
        INNER JOIN shipping_orders o ON o.id = r.order_id
        INNER JOIN users cu ON cu.id = r.customer_id
        WHERE r.driver_profile_id = @DriverProfileId
          AND (@Stars IS NULL OR r.rating_stars = @Stars)
        ORDER BY r.created_at DESC, r.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<DriverRatingsResponse> Handle(
        GetDriverRatingsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var context = await DriverAccess.LoadReadContextAsync(connection, userId, cancellationToken);

        if (context.DriverProfileId is not { } driverProfileId)
        {
            return new DriverRatingsResponse(
                AverageStars: null,
                TotalRatings: 0,
                FiveStarCount: 0,
                FourStarCount: 0,
                ThreeStarCount: 0,
                TwoStarCount: 0,
                OneStarCount: 0,
                Reviews: new PagedResult<DriverRatingItemResponse>([], request.Page, request.PageSize, 0));
        }

        var summary = await connection.QuerySingleAsync<SummaryRow>(
            new CommandDefinition(SummarySql, new { DriverProfileId = driverProfileId }, cancellationToken: cancellationToken));

        var parameters = new
        {
            DriverProfileId = driverProfileId,
            request.Stars,
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        var filteredTotal = request.Stars.HasValue
            ? await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(FilteredCountSql, parameters, cancellationToken: cancellationToken))
            : summary.TotalRatings;

        var rows = await connection.QueryAsync<ItemRow>(
            new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new DriverRatingItemResponse(
            r.Id,
            r.OrderId,
            r.OrderCode,
            r.RatingStars,
            r.ReviewComment,
            r.CustomerName,
            SqlHelpers.ToUtc(r.CreatedAt))).ToList();

        var average = summary.TotalRatings > 0 && summary.AverageStars is { } avg
            ? Math.Round(avg, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        return new DriverRatingsResponse(
            average,
            summary.TotalRatings,
            summary.FiveStarCount,
            summary.FourStarCount,
            summary.ThreeStarCount,
            summary.TwoStarCount,
            summary.OneStarCount,
            new PagedResult<DriverRatingItemResponse>(items, request.Page, request.PageSize, filteredTotal));
    }

    private sealed class SummaryRow
    {
        public int TotalRatings { get; init; }
        public decimal? AverageStars { get; init; }
        public int FiveStarCount { get; init; }
        public int FourStarCount { get; init; }
        public int ThreeStarCount { get; init; }
        public int TwoStarCount { get; init; }
        public int OneStarCount { get; init; }
    }

    private sealed class ItemRow
    {
        public long Id { get; init; }
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public int RatingStars { get; init; }
        public string? ReviewComment { get; init; }
        public string CustomerName { get; init; } = "";
        public DateTime CreatedAt { get; init; }
    }
}

