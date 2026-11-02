using System.Text.Json;
using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Orders.Queries;

namespace ModMove.Application.Features.Refunds.Queries.GetMyRefundDetail;

internal sealed class GetMyRefundDetailQueryHandler(ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser)
    : IQueryHandler<GetMyRefundDetailQuery, MyRefundDetailResponse>
{
    private const string Sql = $"""
        SELECT {RefundRow.Columns}
        {RefundRow.FromCustomerRefunds}
          AND r.id = @RefundId
        """;

    public async Task<MyRefundDetailResponse> Handle(GetMyRefundDetailQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        await CustomerAccess.EnsureActiveAsync(connection, customerId, cancellationToken);

        var r = await connection.QuerySingleOrDefaultAsync<RefundRow>(
            new CommandDefinition(Sql, new { CustomerId = customerId, request.RefundId }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException("Refund", request.RefundId);

        return new MyRefundDetailResponse(
            r.Id, r.OrderId, r.OrderCode, r.IncidentId, r.SourceType, r.RefundStatus, r.Reason, r.ReasonCode,
            r.EvidenceUrls is null ? [] : JsonSerializer.Deserialize<List<string>>(r.EvidenceUrls) ?? [],
            r.RequestedAmount, r.ApprovedAmount, r.DecisionReason, r.InfoRequestNote, r.CustomerReply, r.WaitingForReply,
            CustomerAccess.Utc(r.CreatedAt),
            r.DecidedAt is { } decided ? CustomerAccess.Utc(decided) : null,
            r.ProcessedAt is { } processed ? CustomerAccess.Utc(processed) : null);
    }
}
