using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Tracking.Queries.IsActiveAccount;

internal sealed class IsActiveAccountQueryHandler(ISqlConnectionFactory connectionFactory) : IQueryHandler<IsActiveAccountQuery, bool>
{
    private const string Sql = "SELECT account_status AS AccountStatus FROM users WHERE id = @UserId";

    public async Task<bool> Handle(IsActiveAccountQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var status = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(Sql, new { request.UserId }, cancellationToken: cancellationToken));
        return status == "Active";
    }
}
