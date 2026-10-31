using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Tracking.Queries.CanFollowOrder;

internal sealed class CanFollowOrderQueryHandler(ISqlConnectionFactory connectionFactory) : IQueryHandler<CanFollowOrderQuery, bool>
{
    private const string AccountSql = "SELECT account_status AS AccountStatus FROM users WHERE id = @UserId";
    private const string OrderSql = "SELECT id AS Id FROM shipping_orders WHERE id = @OrderId AND customer_id = @UserId";

    public async Task<bool> Handle(CanFollowOrderQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var parameters = new { request.UserId, request.OrderId };

        var status = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(AccountSql, parameters, cancellationToken: cancellationToken));
        if (status != "Active")
            return false;

        var id = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(OrderSql, parameters, cancellationToken: cancellationToken));
        return id is not null;
    }
}
