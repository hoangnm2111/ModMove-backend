using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Orders.Queries;

namespace ModMove.Application.Features.Refunds.Queries.GetMyRefunds;

internal sealed class GetMyRefundsQueryHandler(ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser)
    : IQueryHandler<GetMyRefundsQuery, PagedResult<MyRefundSummaryResponse>>
{
    private const string CountSql = $"SELECT COUNT(1) {RefundRow.FromCustomerRefunds}";

    private const string ItemsSql = $"""
        SELECT {RefundRow.Columns}
        {RefundRow.FromCustomerRefunds}
        ORDER BY r.created_at DESC, r.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<MyRefundSummaryResponse>> Handle(GetMyRefundsQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        await CustomerAccess.EnsureActiveAsync(connection, customerId, cancellationToken);

        var parameters = new { CustomerId = customerId, request.PageSize, Offset = (request.Page - 1) * request.PageSize };
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<RefundRow>(new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new MyRefundSummaryResponse(
            r.Id, r.OrderId, r.OrderCode, r.SourceType, r.RefundStatus, r.RequestedAmount, r.ApprovedAmount,
            r.WaitingForReply, CustomerAccess.Utc(r.CreatedAt))).ToList();
        return new PagedResult<MyRefundSummaryResponse>(items, request.Page, request.PageSize, total);
    }
}
